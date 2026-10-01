---
title: "ADR 0005: live game updates over SSE with per-player snapshots and an opaque view version"
tags: [adr, game, sse, server-sent-events, live-updates, stream, fog-of-war, view-version, ids, privacy, rate-limit, game-log]
code: [src/Govern.Application/Games/Contracts/WatchGameContract.cs, src/Govern.Api/Features/Games/WatchGameEndpoint.cs, src/Govern.Api/Extensions/AsyncEnumerableExtensions.cs, src/Govern.Infrastructure/Realtime, src/Govern.Core/Visibility/VisibilityCalculator.cs, src/Govern.Core/State/GameState.cs]
updated: 2026-10-01
---

# ADR 0005: live game updates over SSE

## Status
Accepted (2026-09-28).

## Context
Players need to see the opponent join, the game start and the opponent's visible moves without polling. The game is turn-based, runs on one Azure Web App instance, and hides information behind fog of war. Two values in the view also leaked hidden activity: `PlayerView.Version` counted every command, and unit and city ids came from one shared counter.

## Decision
- **Transport:** server-sent events (`GET /api/games/{id}/stream`, `TypedResults.ServerSentEvents`). Updates only flow server → client, and commands stay plain `POST`s. SignalR/WebSockets would add a second protocol for no gain. Clients use `HttpClient` streaming, so the bearer token stays in the `Authorization` header (the browser `EventSource` can't send it).
- **Payload:** the full `GetGameResponse` snapshot per change, the same JSON as `GET /api/games/{id}`. Not engine events: those describe the full state and would need per-player filtering. A reconnect needs no resume logic, because the first event is always the current snapshot.
- **Fan-out:** `Game` raises `GameChangedEvent`, and after the save the event handler signals an in-process hub (`IGameChangeService`, one-slot channels that coalesce bursts). Each stream then re-reads the game for its own player. Signals carry no game data, so nothing crosses between players.
- **Push only visible changes:** a stream sends a snapshot only when the caller's phase, seats or view version changed. A hidden enemy move produces no event, so the stream doesn't leak its timing. (The game list's `updatedAt` still does; see Consequences.)
- **Opaque view version:** `PlayerView.Version` is a SHA-256 fingerprint of the viewer's own view, still a `long`, so the `expectedVersion` contract didn't change. The stale-command check compares it with the caller's current view.
- **Random ids:** unit and city ids are 31 bits of the SHA-256 of a draw from the game's `DeterministicRandom`, never the raw draw: SplitMix64 is invertible, and raw ids let a player rebuild the generator state in seconds (found in review and proven by `GameFactoryTests.Create_Ids_AreNotRawGeneratorDraws`). `NextId` is gone, and `GameState` schema v2 upgrades v1 on read. Views list units and cities by id, not by creation.
- **Connection hygiene:**
  - a heartbeat event after 20 s of silence (Azure closes idle requests at about 230 s);
  - the stream ends at the access token's `exp`, so a revoked session can't keep watching beyond one token lifetime;
  - a per-user `game-streams` policy caps open streams (4) and openings per minute (30). The limiters are chained by `OwningChainedRateLimiter`, which disposes them and stays non-idle while a stream holds a permit (the built-in chain reports idle once the window refills, which let the partition be recreated and the open-stream limit reset).

## Consequences
- Scale-out beyond one Api instance needs a backplane (Postgres `LISTEN/NOTIFY` is the planned one); until then the hub only reaches streams on the same instance. Integration tests hit this too: a stream only wakes for commands sent through the same test host.
- Each change costs one read per open stream of that game (two players, a few tabs). That's fine for turn-based play, and the snapshot is small.
- Clients can't order views by `version`; they keep the latest snapshot they received. A command based on an out-of-date view gets `game.stale_version`.
- Engine events stay server-side until a game-log UI needs them, together with a `VisibilityCalculator.EventsFor`. **Update (2026-10-01, F11a):** that log exists. The snapshot still is the payload, and now also carries the caller's recent log (`GetGameResponse.log`), built from events filtered by `EventsFor` and stored per seat; no raw event is ever sent ([games](../features/games.md#game-log)).
- After the first event, the response has started: a failure while re-reading the game (e.g. the database is down) ends the connection and is logged by ASP.NET Core, not by the mediator pipelines. The client reconnects.
- After the v1 → v2 upgrade, a client holding a pre-upgrade `expectedVersion` gets one `game.stale_version`.
- The list no longer leaks hidden activity: since Plan 3c `GET /api/games` returns and sorts by `activityAt`, which moves only on changes both players see.
- **Client reconnect policy** (`GameWatchService`, see [client game lobby](../features/client.md#live-updates-reconnect-and-backoff)): after a normal end of a connection that was open at least 10 s after its first snapshot (token expiry) the client reconnects in 2–5 s with a freshly refreshed token; other failures back off exponentially with a wide random spread (between half the step and the step + 1 s, steps 2 s up to 30 s), a 429 waits 15–20 s, and 50 s without any event (two missed heartbeats) counts as a dead connection. Every delay is at least 2 s, keeping a tab under the 30 openings per minute. 404 and 401 stop the watch, and so does the end after the `finished` snapshot.
