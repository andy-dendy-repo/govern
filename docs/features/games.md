---
title: Games feature (lobby, invite codes, start, player views, game list)
tags: [games, game, lobby, invite, invite-code, start, player-view, fog-of-war, jsonb, concurrency, privacy]
code: [src/Govern.Application/Games, src/Govern.Api/Features/Games, src/Govern.Domain/Entities/Game.cs, src/Govern.Domain/Entities/GamePlayer.cs, src/Govern.Domain/ValueObjects/InviteCode.cs, src/Govern.Infrastructure/Persistence/Configurations/GameConfiguration.cs]
updated: 2026-09-27
---

# Games feature

The Games feature lets a signed-in user create a two-player game, share an invite code, see who joined, start the game and read their own fog-of-war view of the board. The rules themselves live in the Core engine ([game-core](game-core.md)); this feature stores games and exposes them over HTTP. Game commands and live updates come in [Plan 3](../plans/game-delivery.md).

## Game lifecycle (GamePhase)

| Phase | Meaning | Allowed |
|---|---|---|
| `lobby` | Created; waiting for a second player. `state` is null | Join with the invite code; the creator starts once both seats are taken |
| `inProgress` | Started: `GameFactory.Create` built the board with a secret random seed. The creator (seat 0) moves first | Read the view (commands in Plan 3) |
| `finished` | Reserved for Plan 3 (victory or resignation) | Read |

Phases are stored as text (`games.phase`) and sent as camelCase strings. Leaving a lobby, cancelling a game and regenerating an invite code are not supported yet.

## Games endpoints

All endpoints require a bearer token (`.RequireAuthorization()`).

| Method and route | Use case | Success | Errors |
|---|---|---|---|
| `POST /api/games` | `CreateGameContract` | 201 `{ id, inviteCode }`, `Location: /api/games/{id}` | 401; 409 `game.too_many_open_lobbies` |
| `POST /api/games/join` body `{ inviteCode }` | `JoinGameContract` | 200 `{ gameId }` | 400 malformed code; 401; 404 `game.invite_not_found`; 409 `game.already_started`, `game.full` |
| `POST /api/games/{id}/start` | `StartGameContract` | 200 `GetGameResponse` (the caller's view of the new board) | 400 empty id; 401; 403 `game.not_creator`; 404 `game.not_found`; 409 `game.already_started`, `game.not_enough_players` |
| `GET /api/games/{id}` | `GetGameContract` | 200 `GetGameResponse` | 400 empty id; 401; 404 `game.not_found` |
| `GET /api/games?page=1&pageSize=20` | `ListMyGamesContract` | 200 `{ items: [{ id, phase, playerCount, isCreator, updatedAt }], total }` | 400 page outside 1–10,000 or page size outside 1–50; 401 |

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

The Core rule-violation codes (`move.unreachable`, `game.not_your_turn`, …) come from `RuleViolations` and are separate ([game-core](game-core.md)).

## Invite codes (InviteCode)

- 16 bytes from `RandomNumberGenerator`, base64url without padding: always 22 characters from `A–Z a–z 0–9 - _`.
- Codes are case-sensitive and never normalised. Surrounding whitespace, padding or `+`/`/` make a code malformed (400).
- The code is a bearer secret: anyone who has it can take the free seat. That is acceptable for 2 players, because the code is unguessable (128 bits) and the creator sees who joined.
- The code is sent in the request **body** of `POST /api/games/join`, so it never appears in URLs, access logs or `Referer` headers. No log line contains it.
- Only the creator can read the code again through `GET /api/games/{id}`, and only while in the lobby. The joined player never gets it back, and nobody sees it once the game starts.

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
| `games` | `id`, `creator_id`, `invite_code` (varchar 22), `phase` (varchar 16), `revision` (concurrency token), `state` (`jsonb`, null in the lobby), `created_at`, `updated_at` | Unique `invite_code`; index and restrict FK on `creator_id` → `users` |
| `game_players` | `id` (= `PlayerId`), `game_id`, `user_id`, `seat` (0 or 1), `joined_at` | Unique `(game_id, seat)` and `(game_id, user_id)`; index and restrict FK on `user_id` → `users`; cascade delete from `games` |

- **The `jsonb` state snapshot:** `games.state` holds the whole `GameState`, written and read through `GameSerializer`. That is the same format the Api and client use, and it carries `schemaVersion`.
  - EF compares the state by reference. The state is immutable and replaced on every change, so there is no deep comparison.
  - Queries never look inside the JSON: anything a list needs has its own column. `ListMyGames` never reads `state`.
- **Migration:** `AddGames`.

## Games JSON format

`AddApi` registers `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)` for HTTP JSON. Enums (`phase`, and every enum inside `view`) are therefore camelCase strings, and the `view` object is byte-for-byte the `GameSerializer` format that storage and the future SSE stream use.

## Games tests

- **Unit** (`tests/Govern.UnitTests/Domain`):
  - `InviteCodeTests`: shape, uniqueness, round-trip and malformed input.
  - `GameTests`: seats, idempotent join, start order, no user ids in the state, revision increments.
- **Integration** (`tests/Govern.IntegrationTests/Features/Games`): every endpoint's happy path, 400, 401, 403/404 and each error code. Also covered:
  - concurrent joins (two users; the same user twice) and concurrent starts;
  - the `jsonb` column type;
  - the `view` JSON equals `GameSerializer`;
  - no user ids in responses.
