---
title: Game delivery plan
tags: [plan, game, core, lobby, sse, client, roadmap]
code: [src/Govern.Core]
updated: 2026-09-28
---

# Game delivery plan

Govern is being turned into a 2-player, turn-based hex strategy game. Work is split into five plans, each delivering a working, tested increment.

**Branching for game work:** branch from `gamecore` (`feature/<name>`) and open the PR to `gamecore`, not `stage`. `gamecore` is merged to `stage` when the game is ready.

| Plan | Scope | Status |
|---|---|---|
| 1. Core engine | `Govern.Core`: hex map, settler/soldier, cities and production, alternating turns, attack and capture, victory, fog of war, JSON, determinism. Core brought under all rules | Done ([game-core](../features/game-core.md), [ADR 0004](../decisions/0004-game-engine.md)) |
| 2. Games and lobby | `Game` aggregate (status Lobby/InProgress/Finished, creator, unguessable invite code, version), `game_players` table, `state jsonb` column. Use cases: create game, join by invite code (idempotent, max 2), start (creator only, full lobby → `GameFactory.Create`), get my view, list my games. `PlayerId` = the per-game `GamePlayer.Id`; Core's `GameError` renamed `RuleViolation` | Done ([plan](game-lobby.md), [games](../features/games.md)) |
| 3a. Commands and throttling | `POST /api/games/{id}/commands` with `expectedVersion` (409 `game.stale_version`), actor = the caller's `GamePlayer.Id` (`Game.Execute`), `RuleViolation` → 409 with the same code, denormalised `current_player_id` column and `isYourTurn` in the list, finished phase from the engine. Per-user `games` and `game-commands` rate limits, a 4 KiB command body read with `GameSerializer`, a cap of 20 unfinished games per user (mitigates the Plan 2 Medium audit finding; a rolling quota and lobby leave/expiry remain, see [Plan 2 follow-ups](game-lobby.md)) | Done ([games](../features/games.md#sending-commands)) |
| 3b. Live updates | `GET /api/games/{id}/stream` (SSE): `game` snapshots (`GetGameResponse`) pushed only when the caller's view changes, `heartbeat` after 20 s of silence, end on finish and at token expiry; in-process change hub fed by `GameChangedEvent`; per-user `game-streams` limit (4 open, 30 per minute). Activity leak fixed: `PlayerView.Version` is an opaque per-player fingerprint and unit/city ids are drawn from the game RNG (schema v2). Engine events stay server-side | Done ([games](../features/games.md#live-game-updates-sse), [ADR 0005](../decisions/0005-live-updates.md)) |
| 3c. Lobby lifecycle and quota | `POST /api/games/{id}/leave` (joiner) and `DELETE /api/games/{id}` (creator cancels, the lobby is deleted); a background sweep deletes lobbies after 7 days without activity; a rolling quota of 30 seats per user per 24 hours (finished games included); a database storage alert (`ALERT_EMAIL`); the game list sorts by `activityAt`, which moves only on changes both players see. Deleted games now notify their streams. Resolves the Plan 2/3a/3b follow-ups | Done ([games](../features/games.md#game-lifecycle-gamephase)) |
| 4. Client lobby | Needs the client sign-in, delivered first through `gamecore` as client Plan 7 ([client authentication](../features/client.md#client-authentication-sign-in-and-token-refresh)). Create/join pages, invite link, leave/cancel, SSE client (`HttpClient` streaming with the bearer header + `SseParser`, reconnect with backoff and a fresh token; every connection starts with the current snapshot, so no gap detection) | Next |
| 5. Client board | SVG hex board component (pan/zoom), unit/city icons, selection, move highlights (add a `PlayerView`-based reachability overload to Core: the client only has the view), end turn, game-over screen | Planned |

## Deferred ideas
- Builder unit and terrain improvements; more terrain types (hills, forest, water).
- Strength/HP combat using `DeterministicRandom`.
- Turn timers and handling of absent players.
- Player-chosen city names; game log UI from events filtered by visibility.
- Decide whether a visible enemy city's `Producing`/`ProductionProgress` is public; if not, project a `CityView` in `ViewFor`.
- Trim `PlayerView.Tiles` on the wire (coordinates follow from the row-major index); the stream sends the same `GetGameResponse`, so do it before the client depends on the shape.
- Filtered engine events (`VisibilityCalculator.EventsFor`) for animations and a game log.
- Scale-out beyond one Api instance (Postgres `LISTEN/NOTIFY` as the SSE backplane).
