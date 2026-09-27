---
title: Game delivery plan
tags: [plan, game, core, lobby, sse, client, roadmap]
code: [src/Govern.Core]
updated: 2026-09-27
---

# Game delivery plan

Govern is being turned into a 2-player, turn-based hex strategy game. Work is split into five plans, each delivering a working, tested increment.

**Branching for game work:** branch from `gamecore` (`feature/<name>`) and open the PR to `gamecore`, not `stage`. `gamecore` is merged to `stage` when the game is ready.

| Plan | Scope | Status |
|---|---|---|
| 1. Core engine | `Govern.Core`: hex map, settler/soldier, cities and production, alternating turns, attack and capture, victory, fog of war, JSON, determinism. Core brought under all rules | Done ([game-core](../features/game-core.md), [ADR 0004](../decisions/0004-game-engine.md)) |
| 2. Games and lobby | `Game` aggregate (status Lobby/InProgress/Finished, creator, unguessable invite code, version), `game_players` table, `state jsonb` column. Use cases: create game, join by invite code (idempotent, max 2), start (creator only, full lobby → `GameFactory.Create`), get my view, list my games. `PlayerId` = the per-game `GamePlayer.Id`; Core's `GameError` renamed `RuleViolation` | Done ([plan](game-lobby.md), [games](../features/games.md)) |
| 3a. Commands and throttling | `POST /api/games/{id}/commands` with `expectedVersion` (409 `game.stale_version`), actor = the caller's `GamePlayer.Id` (`Game.Execute`), `RuleViolation` → 409 with the same code, denormalised `current_player_id` column and `isYourTurn` in the list, finished phase from the engine. Per-user `games` and `game-commands` rate limits, a 4 KiB command body read with `GameSerializer`, a cap of 20 unfinished games per user (mitigates the Plan 2 Medium audit finding; a rolling quota and lobby leave/expiry remain, see [Plan 2 follow-ups](game-lobby.md)) | Done ([games](../features/games.md#sending-commands)) |
| 3b. Live updates | SSE stream (`TypedResults.ServerSentEvents`) pushing each player's `PlayerView` with `id: version`, heartbeat every 15–30 s (Azure closes idle requests at ~240 s); a `VisibilityCalculator.EventsFor(update, viewer)` helper if events are pushed; decide the `PlayerView.Version`/`NextId` activity leak before the stream format is frozen (clients only echo `view.version` as `expectedVersion`, so the value can become opaque or per-player) | Next |
| 4. Client lobby | Create/join pages, invite link, SSE client (`HttpClient` streaming with the bearer header + `SseParser`, reconnect with backoff, resync on version gap) | Planned |
| 5. Client board | SVG hex board component (pan/zoom), unit/city icons, selection, move highlights (add a `PlayerView`-based reachability overload to Core: the client only has the view), end turn, game-over screen | Planned |

## Deferred ideas
- Builder unit and terrain improvements; more terrain types (hills, forest, water).
- Strength/HP combat using `DeterministicRandom`.
- Turn timers and handling of absent players.
- Player-chosen city names; game log UI from events filtered by visibility.
- Decide whether a visible enemy city's `Producing`/`ProductionProgress` is public; if not, project a `CityView` in `ViewFor`.
- Trim `PlayerView.Tiles` on the wire (coordinates follow from the row-major index) before the SSE format is frozen.
- Scale-out beyond one Api instance (Postgres `LISTEN/NOTIFY` as the SSE backplane).
