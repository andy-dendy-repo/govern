---
title: Games feature (lobby, invite codes, start, commands, player views, game list, limits)
tags: [games, game, lobby, invite, invite-code, start, commands, expected-version, rate-limit, player-view, fog-of-war, jsonb, concurrency, privacy]
code: [src/Govern.Application/Games, src/Govern.Api/Features/Games, src/Govern.Api/Models/GameCommandBody.cs, src/Govern.Domain/Entities/Game.cs, src/Govern.Domain/Entities/GamePlayer.cs, src/Govern.Domain/ValueObjects/InviteCode.cs, src/Govern.Infrastructure/Persistence/Configurations/GameConfiguration.cs]
updated: 2026-09-27
---

# Games feature

The Games feature lets a signed-in user create a two-player game, share an invite code, see who joined, start the game, send commands (move, attack, found city, set production, end turn, resign) and read their own fog-of-war view of the board. The rules themselves live in the Core engine ([game-core](game-core.md)); this feature stores games and exposes them over HTTP. Live updates (SSE) come in [Plan 3b](../plans/game-delivery.md).

## Game lifecycle (GamePhase)

| Phase | Meaning | Allowed |
|---|---|---|
| `lobby` | Created; waiting for a second player. `state` is null | Join with the invite code; the creator starts once both seats are taken |
| `inProgress` | Started: `GameFactory.Create` built the board with a secret random seed. The creator (seat 0) moves first | Send commands; read the view |
| `finished` | The engine decided the game (victory or resignation). Set by the same command that ended it | Read; every command returns `game.finished` |

Phases are stored as text (`games.phase`) and sent as camelCase strings. Leaving a lobby, cancelling a game and regenerating an invite code are not supported yet.

## Games endpoints

All endpoints require a bearer token (`.RequireAuthorization()`). Create, join and start also answer 429 past the per-user `games` rate limit (see [Games limits](#games-limits)).

| Method and route | Use case | Success | Errors |
|---|---|---|---|
| `POST /api/games` | `CreateGameContract` | 201 `{ id, inviteCode }`, `Location: /api/games/{id}` | 401; 409 `game.too_many_open_lobbies`, `game.too_many_unfinished_games`; 429 |
| `POST /api/games/join` body `{ inviteCode }` | `JoinGameContract` | 200 `{ gameId }` | 400 malformed code; 401; 404 `game.invite_not_found`; 409 `game.already_started`, `game.full`, `game.too_many_unfinished_games`; 429 |
| `POST /api/games/{id}/start` | `StartGameContract` | 200 `GetGameResponse` (the caller's view of the new board) | 400 empty id; 401; 403 `game.not_creator`; 404 `game.not_found`; 409 `game.already_started`, `game.not_enough_players`; 429 |
| `POST /api/games/{id}/commands` body `{ expectedVersion, command }` | `SendGameCommandContract` | 200 `GetGameResponse` (the caller's view after the command) | 400 malformed body or unknown command type, empty id, negative version; 401; 404 `game.not_found`; 409 `game.not_started`, `game.stale_version` or a rule-violation code; 413 body over 4 KiB; 415 not JSON; 429 |
| `GET /api/games/{id}` | `GetGameContract` | 200 `GetGameResponse` | 400 empty id; 401; 404 `game.not_found` |
| `GET /api/games?page=1&pageSize=20` | `ListMyGamesContract` | 200 `{ items: [{ id, phase, playerCount, isCreator, isYourTurn, updatedAt }], total }` | 400 page outside 1–10,000 or page size outside 1–50; 401 |

`GetGameResponse` is `{ id, phase, isCreator, inviteCode, players: [{ playerId, seat, isYou, isCreator }], view, createdAt }`:
- `inviteCode` is set only for the creator, and only while in the lobby.
- `view` is the caller's `PlayerView` once started, and null in the lobby.

## Games error codes (GameErrors)

| Code | Status | When |
|---|---|---|
| `game.too_many_open_lobbies` | 409 | The caller already created `GameErrors.MaxOpenLobbiesPerUser` (5) games that are still in the lobby. The count is not locked, so simultaneous creates can overshoot it (a soft cap; see Risks in the plan) |
| `game.invite_not_found` | 404 | No game uses the invite code |
| `game.not_found` | 404 | The game does not exist **or** the caller does not play in it. The two are deliberately indistinguishable |
| `game.not_creator` | 403 | A player other than the creator tried to start |
| `game.already_started` | 409 | Join or start after the game left the lobby |
| `game.full` | 409 | Both seats are taken |
| `game.not_enough_players` | 409 | Start with only the creator seated |
| `game.too_many_unfinished_games` | 409 | Create or join (as a new player) while the caller already plays in `GameErrors.MaxUnfinishedGamesPerUser` (20) games that are not finished, joined lobbies included. A soft cap, like the lobby cap |
| `game.not_started` | 409 | A command for a game still in the lobby |
| `game.stale_version` | 409 | `expectedVersion` is not the game's current version, or another command was saved first. Reload the game and decide again |

The Core rule-violation codes (`move.unreachable`, `game.not_your_turn`, `game.finished`, …) come from `RuleViolations` ([game-core](game-core.md)). `GameErrors.FromViolation` returns them as 409 with the same code and message. Both sets use the `game.` prefix, so `GameErrorCodesTests` checks that no code appears in both.

## Invite codes (InviteCode)

- 16 bytes from `RandomNumberGenerator`, base64url without padding: always 22 characters from `A–Z a–z 0–9 - _`.
- Codes are case-sensitive and never normalised. Surrounding whitespace, padding or `+`/`/` make a code malformed (400).
- The code is a bearer secret: anyone who has it can take the free seat. That is acceptable for 2 players, because the code is unguessable (128 bits) and the creator sees who joined.
- The code is sent in the request **body** of `POST /api/games/join`, so it never appears in URLs, access logs or `Referer` headers. No log line contains it.
- Only the creator can read the code again through `GET /api/games/{id}`, and only while in the lobby. The joined player never gets it back, and nobody sees it once the game starts.

## Sending commands

- **Body:** `{ "expectedVersion": 3, "command": { "type": "move_unit", "unitId": { "value": 2 }, "target": { "q": 4, "r": 1 } } }`. Command shapes and `type` names are the Core `GameCommand` types (`move_unit`, `attack`, `found_city`, `set_production`, `end_turn`, `resign`).
- **Read with `GameSerializer`:** the body is bound by `Api/Models/GameCommandBody.BindAsync`, not by the HTTP JSON defaults. `type` may appear in any position, and a missing `type` or value is a 400 instead of a server error. The body is capped at 4 KiB (413), and a non-JSON content type is 415.
- **`expectedVersion`:** send the `view.version` you last saw. If the game has moved on, the command is not applied (`game.stale_version`). This also settles races: two commands based on the same version race on `games.revision`, and the loser gets `game.stale_version`.
- **Checks in order:** membership (404) → lobby (`game.not_started`) → version (`game.stale_version`) → the engine (`GameEngine.Execute` through `Game.Execute`), whose violations become 409 with their own code.
- **Result:** an accepted command replaces `games.state`, copies the player to move into `games.current_player_id`, bumps `revision`, and sets `phase` to `finished` once the engine reports a winner. The response is the caller's new `GetGameResponse`. Events aren't returned yet; they need visibility filtering (Plan 3b).
- Resigning is allowed out of turn. A command for a finished game returns `game.finished`.

## Games limits

| Limit | Value | Where |
|---|---|---|
| `games` rate-limit policy | 20 requests per user per minute, shared by create, join and start (`RateLimiting:GamesPermitLimit` / `GamesWindow`) | Fixed window partitioned by the JWT `sub` |
| `game-commands` rate-limit policy | 120 commands per user per minute (`RateLimiting:GameCommandsPermitLimit` / `GameCommandsWindow`) | Fixed window partitioned by the JWT `sub` |
| Open lobbies created | 5 per user (`GameErrors.MaxOpenLobbiesPerUser`) | `CreateGameHandler` |
| Unfinished games played in | 20 per user (`GameErrors.MaxUnfinishedGamesPerUser`) | `CreateGameHandler`, `JoinGameHandler` (new players only) |
| Command body | 4 KiB (`GameCommandBody.MaxBytes`) | `GameCommandBody.BindAsync` |

**Known gap (lock-out):** joined lobbies count toward the unfinished cap, but a lobby can't be left, cancelled or resigned and never expires. A user who joins 20 lobbies that are never started can't create or join again until they start. Lobby leave/cancel/expiry is a tracked follow-up ([game-lobby](../plans/game-lobby.md)). Dropping lobbies from the count is not the fix: `Start` doesn't check the cap, so creators could then push a joiner past 20 running games.

The rate limiter runs after authentication, so every game request has a user. The counts behind the caps aren't locked: a burst of simultaneous requests can overshoot a cap by up to the rate-limit permit count (20). The rate limit bounds how far.

**Known gap (storage):** finished games are neither capped nor deleted, and `resign` finishes a game at once, so a pair of accounts can still add games indefinitely, only slower. A rolling per-user quota is an open follow-up ([game-lobby](../plans/game-lobby.md)). Rate-limit counters are in memory, one set per Api instance.

## Player identity and privacy

- Each seat (`GamePlayer`) has its own id, and that id is the engine's `PlayerId`. It appears in `PlayerView.Viewer`, `CurrentPlayer`, `Winner`, unit and city owners, and `players[].playerId`.
- User ids and emails are never sent to other players. Responses contain no user ids at all, and an integration test checks the raw JSON.
- The client tells seats apart through `players[].playerId` and `isYou`. Display names are not supported yet: users only have an email, which must not be shown to an invite holder.

## Joining concurrently

- **Friendly checks first:** `JoinGameHandler` checks whether the caller is already seated (then it succeeds and changes nothing), whether the game has started, and whether it is full.
- **The database settles races:**
  - `game_players` has unique `(game_id, seat)` and `(game_id, user_id)` indexes.
  - `games.revision` is an optimistic concurrency token that every join and start increments.
- **The loser re-reads once:** a request that loses a race gets a unique violation or a `DbUpdateConcurrencyException`. It then reads the game again and returns what the winner left:
  - `game.full` when another user took the seat;
  - success when the same user joined twice at once.
- **Start races:** two simultaneous starts race on `revision`, and the loser gets `game.already_started`.

## Games data model

| Table | Columns | Constraints |
|---|---|---|
| `games` | `id`, `creator_id`, `invite_code` (varchar 22), `phase` (varchar 16), `revision` (concurrency token), `state` (`jsonb`, null in the lobby), `current_player_id` (the `game_players.id` to move; null in the lobby and once finished; no FK), `created_at`, `updated_at` | Unique `invite_code`; index and restrict FK on `creator_id` → `users` |
| `game_players` | `id` (= `PlayerId`), `game_id`, `user_id`, `seat` (0 or 1), `joined_at` | Unique `(game_id, seat)` and `(game_id, user_id)`; index and restrict FK on `user_id` → `users`; cascade delete from `games` |

- **The `jsonb` state snapshot:** `games.state` holds the whole `GameState`, written and read through `GameSerializer`. That is the same format the Api and client use, and it carries `schemaVersion`.
  - EF compares the state by reference. The state is immutable and replaced on every change, so there is no deep comparison.
  - Queries never look inside the JSON: anything a list needs has its own column. `ListMyGames` never reads `state`; `isYourTurn` compares `current_player_id` with the caller's seat.
- **Migrations:** `AddGames`, `AddGameCurrentPlayer`.

## Games JSON format

`AddApi` registers `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)` for HTTP JSON. Enums (`phase`, and every enum inside `view`) are therefore camelCase strings, and the `view` object is byte-for-byte the `GameSerializer` format that storage and the future SSE stream use.

## Games tests

- **Unit** (`tests/Govern.UnitTests/Domain`):
  - `InviteCodeTests`: shape, uniqueness, round-trip and malformed input.
  - `GameTests`: seats, idempotent join, start order, no user ids in the state, revision increments, `Execute` (accepted, rejected, resign finishes, lobby and outsider throw).
- **Integration** (`tests/Govern.IntegrationTests/Features/Games`): every endpoint's happy path, 400, 401, 403/404 and each error code. Also covered:
  - concurrent joins (two users; the same user twice), concurrent starts and concurrent commands with the same version;
  - the command body (type in any position, malformed, 413, 415), stale versions and rule-violation codes;
  - both rate-limit policies (per user) and the unfinished-games cap;
  - the `jsonb` column type;
  - the `view` JSON equals `GameSerializer`;
  - no user ids in responses.
