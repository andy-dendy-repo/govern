---
title: Games feature (lobby, invite codes, leave, cancel, lobby expiry, start, commands, live updates, player views, game list, limits, quota)
tags: [games, game, lobby, invite, invite-code, leave, cancel, expiry, quota, activity-at, start, commands, expected-version, live-updates, sse, server-sent-events, stream, heartbeat, rate-limit, player-view, fog-of-war, jsonb, concurrency, privacy]
code: [src/Govern.Application/Games, src/Govern.Api/Features/Games, src/Govern.Api/Models/GameCommandBody.cs, src/Govern.Api/Extensions/AsyncEnumerableExtensions.cs, src/Govern.Infrastructure/Realtime, src/Govern.Infrastructure/Background/ExpiredLobbyCleanupService.cs, src/Govern.Domain/Entities/Game.cs, src/Govern.Domain/Entities/GamePlayer.cs, src/Govern.Domain/ValueObjects/InviteCode.cs, src/Govern.Infrastructure/Persistence/Configurations/GameConfiguration.cs]
updated: 2026-09-29
---

# Games feature

The Games feature lets a signed-in user create a two-player game, share an invite code, see who joined, leave or cancel a lobby, start the game, send commands (move, attack, found city, set production, end turn, resign), read their own fog-of-war view of the board and watch it change live over server-sent events. The rules themselves live in the Core engine ([game-core](game-core.md)); this feature stores games and exposes them over HTTP.

## Game lifecycle (GamePhase)

| Phase | Meaning | Allowed |
|---|---|---|
| `lobby` | Created; waiting for a second player. `state` is null | Join with the invite code; the joiner leaves; the creator cancels (deletes) it, or starts once both seats are taken. Deleted after 7 days without activity |
| `inProgress` | Started: `GameFactory.Create` built the board with a secret random seed. The creator (seat 0) moves first | Send commands; read the view |
| `finished` | The engine decided the game (victory or resignation). Set by the same command that ended it | Read; every command returns `game.finished` |

Phases are stored as text (`games.phase`) and sent as camelCase strings. A cancelled or expired lobby is deleted, not given a phase: its rows and invite code are gone, and GET returns 404. Leaving a running game is done by resigning. Regenerating an invite code is not supported yet.

- **Leave** (`Game.Leave`): only a joined player, only in the lobby. The seat row is deleted, the creator sees a one-seat lobby again, and the leaver may rejoin with the code (seat 1 again).
- **Cancel** (`Game.Cancel`, then the handler deletes the game): only the creator, only in the lobby. The creator can't leave their own lobby (`game.creator_cannot_leave`).
- **Expiry:** `Infrastructure/Background/ExpiredLobbyCleanupService` runs every `LobbyCleanup:Interval` (1 hour) and deletes lobbies whose `activity_at` is older than `Game.LobbyLifetime` (7 days), in batches of `LobbyCleanup:BatchSize` (500). The delete repeats the predicate, so a lobby joined in between survives. It uses `ExecuteDeleteAsync` (no domain events), so it signals the deleted games' watchers itself. An expired lobby lingers for up to one interval.

## Games endpoints

All endpoints require a bearer token (`.RequireAuthorization()`). Create, join, start, leave and cancel also answer 429 past the per-user `games` rate limit (see [Games limits](#games-limits)).

| Method and route | Use case | Success | Errors |
|---|---|---|---|
| `POST /api/games` | `CreateGameContract` | 201 `{ id, inviteCode }`, `Location: /api/games/{id}` | 401; 409 `game.too_many_open_lobbies`, `game.too_many_unfinished_games`; 429 (rate limit, or `game.daily_quota_reached`) |
| `POST /api/games/join` body `{ inviteCode }` | `JoinGameContract` | 200 `{ gameId }` | 400 malformed code; 401; 404 `game.invite_not_found`; 409 `game.already_started`, `game.full`, `game.too_many_unfinished_games`; 429 (rate limit, or `game.daily_quota_reached`) |
| `POST /api/games/{id}/leave` | `LeaveGameContract` | 204 | 400 empty id; 401; 404 `game.not_found`; 409 `game.creator_cannot_leave`, `game.already_started`, `game.stale_version`; 429 |
| `DELETE /api/games/{id}` | `CancelGameContract` | 204 (the lobby is deleted) | 400 empty id; 401; 403 `game.not_creator`; 404 `game.not_found`; 409 `game.already_started`, `game.stale_version`; 429 |
| `POST /api/games/{id}/start` | `StartGameContract` | 200 `GetGameResponse` (the caller's view of the new board) | 400 empty id; 401; 403 `game.not_creator`; 404 `game.not_found`; 409 `game.already_started`, `game.not_enough_players`, `game.stale_version`; 429 |
| `POST /api/games/{id}/commands` body `{ expectedVersion, command }` | `SendGameCommandContract` | 200 `GetGameResponse` (the caller's view after the command) | 400 malformed body or unknown command type, empty id, negative version; 401; 404 `game.not_found`; 409 `game.not_started`, `game.stale_version` or a rule-violation code; 413 body over 4 KiB; 415 not JSON; 429 |
| `GET /api/games/{id}` | `GetGameContract` | 200 `GetGameResponse` | 400 empty id; 401; 404 `game.not_found` |
| `GET /api/games/{id}/stream` | `WatchGameContract` | 200 `text/event-stream` of `game` (a `GetGameResponse`) and `heartbeat` events (see [Live game updates](#live-game-updates-sse)) | 400 empty id; 401; 404 `game.not_found`; 429 |
| `GET /api/games?page=1&pageSize=20` | `ListMyGamesContract` | 200 `{ items: [{ id, phase, playerCount, isCreator, isYourTurn, activityAt }], total }` | 400 page outside 1–10,000 or page size outside 1–50; 401 |

The list is ordered by `activityAt`, newest first: the last change both players see (created, a seat taken or left, the start, a turn change, the finish). A move or production change leaves it alone, so the list reveals nothing about the opponent's hidden activity. It is stored in `games.activity_at` and set by `Game`; `updated_at` is only the audit column and is never returned.

`GetGameResponse` is `{ id, phase, isCreator, inviteCode, players: [{ playerId, seat, isYou, isCreator }], view, createdAt }`:
- `inviteCode` is set only for the creator, and only while in the lobby.
- `view` is the caller's `PlayerView` once started, and null in the lobby.

## Games error codes (GameErrors)

| Code | Status | When |
|---|---|---|
| `game.too_many_open_lobbies` | 409 | The caller already created `GameErrors.MaxOpenLobbiesPerUser` (5) games that are still in the lobby. The count is not locked, so simultaneous creates can overshoot it (a soft cap; see Risks in the plan) |
| `game.invite_not_found` | 404 | No game uses the invite code |
| `game.not_found` | 404 | The game does not exist **or** the caller does not play in it. The two are deliberately indistinguishable |
| `game.not_creator` | 403 | A player other than the creator tried to start or cancel |
| `game.creator_cannot_leave` | 409 | The creator tried to leave their own lobby; they cancel it instead |
| `game.already_started` | 409 | Join, start, leave or cancel after the game left the lobby |
| `game.full` | 409 | Both seats are taken |
| `game.not_enough_players` | 409 | Start with only the creator seated |
| `game.too_many_unfinished_games` | 409 | Create or join (as a new player) while the caller already plays in `GameErrors.MaxUnfinishedGamesPerUser` (20) games that are not finished, joined lobbies included. A soft cap, like the lobby cap. Leaving or cancelling a lobby frees a slot |
| `game.daily_quota_reached` | 429 | Create or join (as a new player) after the caller took `GameErrors.MaxGamesJoinedPerWindow` (30) seats in the last 24 hours (`GameErrors.QuotaWindow`), finished games included. A soft cap |
| `game.not_started` | 409 | A command for a game still in the lobby |
| `game.stale_version` | 409 | `expectedVersion` is not the caller's current `view.version` (their view changed since), or another command was saved first. Also returned when a start, leave or cancel lost a race and the game still allows it (e.g. the joiner left and someone else joined). Reload the game (or take the latest stream snapshot) and decide again |

The Core rule-violation codes (`move.unreachable`, `game.not_your_turn`, `game.finished`, …) come from `RuleViolations` ([game-core](game-core.md)). `GameErrors.FromViolation` returns them as 409 with the same code and message. Both sets use the `game.` prefix, so `GameErrorCodesTests` checks that no code appears in both.

## Invite codes (InviteCode)

- 16 bytes from `RandomNumberGenerator`, base64url without padding: always 22 characters from `A–Z a–z 0–9 - _`.
- Codes are case-sensitive and never normalised. Surrounding whitespace, padding or `+`/`/` make a code malformed (400).
- The code is a bearer secret: anyone who has it can take the free seat. That is acceptable for 2 players, because the code is unguessable (128 bits) and the creator sees who joined.
- The code is sent in the request **body** of `POST /api/games/join`, so it never appears in URLs, access logs or `Referer` headers. No log line contains it.
- Only the creator can read the code again through `GET /api/games/{id}`, and only while in the lobby. The joined player never gets it back, and nobody sees it once the game starts.
- **Invite link:** the client shares `https://<client>/join#<code>`. The code sits in the URL **fragment**, which browsers never send to a server (not to the Static Web App, not in `Referer`). The join page removes the fragment from the address bar and the Back history at once, fills in its form, and posts the code in the body only when the player clicks Join (a link alone never joins). The browser's global history still records the visited link. See [client game lobby](client.md#invite-link-policy-code-only-in-the-fragment-or-the-body).

## Sending commands

- **Body:** `{ "expectedVersion": 3, "command": { "type": "move_unit", "unitId": { "value": 2 }, "target": { "q": 4, "r": 1 } } }`. Command shapes and `type` names are the Core `GameCommand` types (`move_unit`, `attack`, `found_city`, `set_production`, `end_turn`, `resign`).
- **Read with `GameSerializer`:** the body is bound by `Api/Models/GameCommandBody.BindAsync`, not by the HTTP JSON defaults. `type` may appear in any position, and a missing `type` or value is a 400 instead of a server error. The body is capped at 4 KiB (413), and a non-JSON content type is 415.
- **`expectedVersion`:** send the `view.version` you last saw. It is an opaque, per-player fingerprint of your own view (see [game-core](game-core.md#view-version)): compare it for equality only, never order it. If your view has changed since, the command is not applied (`game.stale_version`); changes hidden from you never make it stale. Races are settled by `games.revision`: two commands based on the same view race, and the loser gets `game.stale_version`.
- **Checks in order:** membership (404) → lobby (`game.not_started`) → version (`game.stale_version`) → the engine (`GameEngine.Execute` through `Game.Execute`), whose violations become 409 with their own code.
- **Result:** an accepted command replaces `games.state`, copies the player to move into `games.current_player_id`, bumps `revision`, and sets `phase` to `finished` once the engine reports a winner. The response is the caller's new `GetGameResponse`. Engine events aren't sent to clients; they would need visibility filtering (deferred to the game-log UI).
- Resigning is allowed out of turn. A command for a finished game returns `game.finished`.

## Games limits

| Limit | Value | Where |
|---|---|---|
| `games` rate-limit policy | 20 requests per user per minute, shared by create, join, start, leave and cancel (`RateLimiting:GamesPermitLimit` / `GamesWindow`) | Fixed window partitioned by the JWT `sub` |
| `game-commands` rate-limit policy | 120 commands per user per minute (`RateLimiting:GameCommandsPermitLimit` / `GameCommandsWindow`) | Fixed window partitioned by the JWT `sub` |
| `game-streams` rate-limit policy | 4 open streams per user (`RateLimiting:GameStreamsConcurrentLimit`), and 30 streams opened per user per minute (`GameStreamsPermitLimit` / `GameStreamsWindow`) | A concurrency limiter chained with a fixed window (`Api/Helpers/OwningChainedRateLimiter`), partitioned by the JWT `sub`; the concurrency permit is held while the stream is open, and the partition is never dropped while a stream holds one |
| Open lobbies created | 5 per user (`GameErrors.MaxOpenLobbiesPerUser`) | `CreateGameHandler` |
| Unfinished games played in | 20 per user (`GameErrors.MaxUnfinishedGamesPerUser`) | `GameSeatLimitService`, from `CreateGameHandler` and `JoinGameHandler` (new players only) |
| Seats taken (rolling quota) | 30 per user in any 24 hours, finished games included (`GameErrors.MaxGamesJoinedPerWindow`, `QuotaWindow`); counted from `game_players.joined_at` | `GameSeatLimitService` |
| Lobby lifetime | 7 days without activity (`Game.LobbyLifetime`), then deleted | `ExpiredLobbyCleanupService` |
| Command body | 4 KiB (`GameCommandBody.MaxBytes`) | `GameCommandBody.BindAsync` |

Joined lobbies count toward the unfinished cap (dropping them would let creators push a joiner past 20 running games, because `Start` doesn't check the cap). A user at the cap leaves or cancels a lobby, and a lobby nobody starts expires after 7 days.

The rate limiter runs after authentication, so every game request has a user. The counts behind the caps aren't locked: a burst of simultaneous requests can overshoot a cap by up to the rate-limit permit count (20). The rate limit bounds how far.

**Storage:** finished games are kept, so the rolling quota bounds how fast one account can add them (at most 30 games, about 13 KB of state each, per 24 hours). A seat given up by leaving or cancelling a lobby no longer counts, which is harmless: a lobby holds no board state, and a started game keeps both seats. An Azure Monitor alert fires when database storage passes 80% ([deployment](../operations/deployment.md)). Rate-limit counters are in memory, one set per Api instance.

## Live game updates (SSE)

`GET /api/games/{id}/stream` is a server-sent events stream (`TypedResults.ServerSentEvents`) of the caller's snapshots of one game. Clients read it with `HttpClient` streaming and the usual `Authorization: Bearer` header; the token is never put in the URL.

| Event type | Data | When |
|---|---|---|
| `game` | A `GetGameResponse`, the same JSON as `GET /api/games/{id}` | First the current snapshot, then each time the caller's phase, seats or view change (a join, a leave, the start, a visible command, the end) |
| `heartbeat` | `null` | After `GameStream:HeartbeatInterval` (20 s) without an event, so proxies and Azure (idle timeout about 230 s) keep the connection and clients can detect a dead one |

- **Push on change, per player:** a command wakes every stream of the game, and each stream reads the game again for its own player. A snapshot is sent only when `(phase, player count, view.version)` changed, so a move hidden by the fog of war sends the opponent nothing (not even an empty event). Events never carry data another player may not see.
- **End of the stream:** the server ends it after the `finished` snapshot, when the caller can no longer read the game (they left the lobby, or it was cancelled or expired), and when the caller's access token expires (its `exp`). The client then refreshes its token and reconnects. There is no `Last-Event-ID` resume: the first event of every connection is the current snapshot, so a reconnect is a resync.
- **Errors before streaming:** membership is checked first, so an outsider or unknown game gets a normal 404 ProblemDetails, not an empty stream. Limits: see [Games limits](#games-limits).
- **How changes travel:** `Game.Join` (a new seat), `Leave`, `Cancel`, `Start` and an accepted `Execute` raise `GameChangedEvent` (the interceptor dispatches the events of deleted entities too). After the save, `GameChangedEventHandler` calls `IGameChangeService.Publish`, and `InMemoryGameChangeService` signals each subscription (a one-slot channel, so bursts coalesce and a slow stream never blocks a command). `WatchGameHandler` subscribes **before** its first read, so no change is missed.
- **Errors after the stream started:** a failure while re-reading the game ends the connection (the client reconnects); it is logged by ASP.NET Core, not by the mediator pipelines.
- **Single instance:** the hub is in process. A second Api instance would not see the first one's changes; scaling out needs a backplane such as Postgres `LISTEN/NOTIFY` (deferred). See [ADR 0005](../decisions/0005-live-updates.md).

## Player identity and privacy

- Each seat (`GamePlayer`) has its own id, and that id is the engine's `PlayerId`. It appears in `PlayerView.Viewer`, `CurrentPlayer`, `Winner`, unit and city owners, and `players[].playerId`.
- User ids and emails are never sent to other players. Responses contain no user ids at all, and an integration test checks the raw JSON.
- The client tells seats apart through `players[].playerId` and `isYou`. Display names are not supported yet: users only have an email, which must not be shown to an invite holder.

## Joining concurrently

- **Friendly checks first:** `JoinGameHandler` checks whether the caller is already seated (then it succeeds and changes nothing), whether the game has started, and whether it is full.
- **The database settles races:**
  - `game_players` has unique `(game_id, seat)` and `(game_id, user_id)` indexes.
  - `games.revision` is an optimistic concurrency token that every join and start increments.
- **The loser re-reads once:** a request that loses a race gets a unique violation, a `DbUpdateConcurrencyException`, or a foreign key violation (the lobby was cancelled or expired under its seat insert; `IDbExceptionClassificationService.IsForeignKeyViolation`). It then reads the game again and returns what the winner left:
  - `game.full` when another user took the seat;
  - `game.invite_not_found` when the lobby was deleted;
  - success when the same user joined twice at once.
- **Start, leave and cancel races:** they race on `revision` (and cancel and leave also on the joiner's seat row). The loser reads the game again once and returns what the winner left: `game.already_started`, `game.not_enough_players` (the joiner left first), `game.not_found` (cancelled first), or `game.stale_version` when the game still allows the action.

## Games data model

| Table | Columns | Constraints |
|---|---|---|
| `games` | `id`, `creator_id`, `invite_code` (varchar 22), `phase` (varchar 16), `revision` (concurrency token), `state` (`jsonb`, null in the lobby), `current_player_id` (the `game_players.id` to move; null in the lobby and once finished; no FK), `activity_at` (last change both players see), `created_at`, `updated_at` | Unique `invite_code`; index `(phase, activity_at)` for the lobby sweep; index and restrict FK on `creator_id` → `users` |
| `game_players` | `id` (= `PlayerId`), `game_id`, `user_id`, `seat` (0 or 1), `joined_at` | Unique `(game_id, seat)` and `(game_id, user_id)`; index `(user_id, joined_at)` (the FK, member filters and the quota) and restrict FK on `user_id` → `users`; cascade delete from `games` |

- **The `jsonb` state snapshot:** `games.state` holds the whole `GameState`, written and read through `GameSerializer`. That is the same format the Api and client use, and it carries `schemaVersion`.
  - EF compares the state by reference. The state is immutable and replaced on every change, so there is no deep comparison.
  - Queries never look inside the JSON: anything a list needs has its own column. `ListMyGames` never reads `state`; `isYourTurn` compares `current_player_id` with the caller's seat.
- **Migrations:** `AddGames`, `AddGameCurrentPlayer`, `AddGameLobbyLifecycle` (existing rows got `activity_at` = 0001-01-01, so lobbies created before it are swept at once and older games sort last; `gamecore` was never deployed, so only local data is affected).

## Games JSON format

`AddApi` registers `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)` for HTTP JSON. Enums (`phase`, and every enum inside `view`) are therefore camelCase strings, and the `view` object is byte-for-byte the `GameSerializer` format that storage uses. In `view`, `tiles` are row-major without coordinates (`{"visibility":"visible","terrain":"plains"}`; the position follows from the index and `width`), and an enemy city's `producing` and `productionProgress` are null ([fog of war](game-core.md#fog-of-war-playerview)). Stream events serialize their data with the same HTTP JSON options.

## Games tests

- **Unit** (`tests/Govern.UnitTests/Domain`):
  - `InviteCodeTests`: shape, uniqueness, round-trip and malformed input.
  - `GameTests`: seats, idempotent join, start order, no user ids in the state, revision increments, `Execute` (accepted, rejected, resign finishes, lobby and outsider throw), `GameChangedEvent` on join, leave, cancel, start and accepted commands only, leave and cancel rules, and `ActivityAt` (moves only on changes both players see).
- **Integration** (`tests/Govern.IntegrationTests/Features/Games`): every endpoint's happy path, 400, 401, 403/404 and each error code. Also covered:
  - the stream (`WatchGameEndpointTests`, read with `Support/GameEventsReader`): the lobby → join → start sequence, both players' snapshots after `end_turn`, no push to the opponent after a hidden move (only heartbeats), the end after a resign or for a finished game, the end at token expiry (`FakeTimeProvider`), and both parts of the `game-streams` limit, and the open-stream limit still holding after the window refilled (a real 13 s wait);
  - concurrent joins (two users; the same user twice), concurrent starts and concurrent commands with the same version;
  - the command body (type in any position, malformed, 413, 415), stale versions and rule-violation codes;
  - both rate-limit policies (per user), the unfinished-games cap and the rolling quota (seeded seats inside and outside the window);
  - leave and cancel (freed caps, rejoin, deleted invite code, streams that end) and their races with start, join and each other (`Support/GameRowLock`; `RaceInOrderAsync` fixes the winner, e.g. a cancel that deletes the lobby under a join's seat insert → `game.invite_not_found`, not a 500);
  - the list order by `activityAt` (a move leaves it alone, `end_turn` moves the game up);
  - the expired-lobby sweep (`Background/ExpiredLobbyCleanupServiceTests`, `FakeTimeProvider`): only stale lobbies go, their streams end;
  - the `jsonb` column type;
  - the `view` JSON equals `GameSerializer`;
  - no user ids in responses.
