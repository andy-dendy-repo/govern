---
title: Game delivery plan
tags: [plan, game, core, lobby, sse, client, roadmap]
code: [src/Govern.Core]
updated: 2026-09-29
---

# Game delivery plan

Govern is being turned into a 2-player, turn-based hex strategy game. Work is split into five plans, each delivering a working, tested increment.

**Branching for game work:** branch from `gamecore` (`feature/<name>`) and open the PR to `gamecore`, not `stage`. `gamecore` is merged to `stage` when the game is ready.

| Plan | Scope | Status |
|---|---|---|
| 1. Core engine | `Govern.Core`: hex map, settler/soldier, cities and production, alternating turns, attack and capture, victory, fog of war, JSON, determinism. Core brought under all rules | Done ([game-core](../features/game-core.md), [ADR 0004](../decisions/0004-game-engine.md)) |
| 2. Games and lobby | `Game` aggregate (status Lobby/InProgress/Finished, creator, unguessable invite code, version), `game_players` table, `state jsonb` column. Use cases: create game, join by invite code (idempotent, max 2), start (creator only, full lobby → `GameFactory.Create`), get my view, list my games. `PlayerId` = the per-game `GamePlayer.Id`; Core's `GameError` renamed `RuleViolation` | Done ([plan](done-game-lobby.md), [games](../features/games.md)) |
| 3a. Commands and throttling | `POST /api/games/{id}/commands` with `expectedVersion` (409 `game.stale_version`), actor = the caller's `GamePlayer.Id` (`Game.Execute`), `RuleViolation` → 409 with the same code, denormalised `current_player_id` column and `isYourTurn` in the list, finished phase from the engine. Per-user `games` and `game-commands` rate limits, a 4 KiB command body read with `GameSerializer`, a cap of 20 unfinished games per user (mitigates the Plan 2 Medium audit finding; a rolling quota and lobby leave/expiry remain, see [Plan 2 follow-ups](done-game-lobby.md)) | Done ([games](../features/games.md#sending-commands)) |
| 3b. Live updates | `GET /api/games/{id}/stream` (SSE): `game` snapshots (`GetGameResponse`) pushed only when the caller's view changes, `heartbeat` after 20 s of silence, end on finish and at token expiry; in-process change hub fed by `GameChangedEvent`; per-user `game-streams` limit (4 open, 30 per minute). Activity leak fixed: `PlayerView.Version` is an opaque per-player fingerprint and unit/city ids are drawn from the game RNG (schema v2). Engine events stay server-side | Done ([games](../features/games.md#live-game-updates-sse), [ADR 0005](../decisions/0005-live-updates.md)) |
| 3c. Lobby lifecycle and quota | `POST /api/games/{id}/leave` (joiner) and `DELETE /api/games/{id}` (creator cancels, the lobby is deleted); a background sweep deletes lobbies after 7 days without activity; a rolling quota of 30 seats per user per 24 hours (finished games included); a database storage alert (`ALERT_EMAIL`); the game list sorts by `activityAt`, which moves only on changes both players see. Deleted games now notify their streams. Resolves the Plan 2/3a/3b follow-ups | Done ([games](../features/games.md#game-lifecycle-gamephase)) |
| 4. Client lobby | Built on the client sign-in, delivered first through `gamecore` as client Plan 7 ([client authentication](../features/client.md#client-authentication-sign-in-and-token-refresh)). `/games` (paged list, new game), `/join` (invite link `/join#<code>`: the code only in the fragment and the POST body, dropped from the URL at once; the link fills the form and joining takes a click; or a pasted link or code), `/games/{id}` (players, invite share with copy, start, cancel, leave, status once started). `GamesClient.WatchAsync` (`HttpClient` streaming with the bearer header + `SseParser`) and `GameWatchService` (reconnect with backoff and a fresh token, 50 s liveness; every connection starts with the current snapshot, so no gap detection). The client ignores `view` until Plan 5 | Done ([client game lobby](../features/client.md#client-game-lobby-games-invite-links-live-updates)) |
| 5. Client board | **5a, Core view for the client:** `TileView` without coordinates (row-major index, `PlayerView.CoordAt`/`TileAt`), enemy production hidden (`CityView`), and view-based previews `GameEngine.ReachableTiles`/`AttackTargets`/`CheckFoundCity` over a `PlayerView`, proven equal to `Execute`. **5b, client board:** `GameTable` on `/games/{id}`: an SVG hex board (`Components/Board`: per-tile clicks, drag to pan, wheel and buttons to zoom, fit; `board.js` only for a non-passive wheel listener and measuring), fog, unit and city icons, selection with move and attack highlights from the Core previews, turn/unit/city panels for all six commands (resign confirmed), one command at a time with `expectedVersion = view.version`, `game.stale_version` → reload and a notice, game-over panel. The client uses Core for views and previews only (arch test) | Done ([game-core](../features/game-core.md#view-previews-for-the-client), [client game board](../features/client.md#client-game-board-hex-map-selection-and-commands)) |

**Status: done.** All five plans are delivered; the game ships through item 1 of [next follow-ups](next-follow-ups.md).

## Deferred ideas

Moved to [next follow-ups](next-follow-ups.md) (item 8; the `UnitView` idea, item 6 there, is done), where they are tracked from now on.
