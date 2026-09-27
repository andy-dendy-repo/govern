---
title: "Game plan 2: games, lobby and invite links"
tags: [plan, game, lobby, invite, persistence, jsonb, ef-core, games]
code: [src/Govern.Domain/Entities, src/Govern.Application/Games, src/Govern.Api/Features/Games, src/Govern.Infrastructure/Persistence/Configurations]
updated: 2026-09-27
---

# Plan: Game plan 2: games, lobby and invite links

Part of the [game delivery plan](game-delivery.md). Branch `feature/game-lobby` from `gamecore` after PR #8 (Core engine) is merged, and open the PR to `gamecore`. Run it through `/develop-feature`.

**Status: implemented** on `feature/game-lobby`. The feature is documented in [games](../features/games.md). The decisions and changes made during implementation are listed under "Decisions made during implementation" at the end.

## Context
The Core engine can play a game in memory, but there is no way to create a game, invite a friend or store a game. This plan adds the `Game` aggregate:
- a lobby phase with a shareable invite code;
- up to two players;
- a creator-only start that calls `GameFactory.Create`;
- the full `GameState` persisted in a Postgres `jsonb` column.

Players can then create a game, send the link, see who joined, start the game and fetch their fog-of-war `PlayerView`. Commands and live updates come in Plan 3.

## Scope
- In:
  - Domain: `Game` + `GamePlayer` entities, the `GamePhase` enum and the `InviteCode` value object.
  - Five use cases: create game, join by invite code, start game, get game (lobby info + `PlayerView`), list my games.
  - EF configuration with the `jsonb` state column and a concurrency token.
  - Migration `AddGames`.
  - Core rename `GameError`/`GameErrors` → `RuleViolation`/`RuleViolations` (see Risks).
  - One JSON format for `PlayerView` in HTTP responses.
- Out (later plans or deferred):
  - Game commands, `ExpectedVersion`, SSE (Plan 3).
  - Client pages (Plan 4).
  - Leaving a lobby, cancelling or deleting a game, regenerating the invite code.
  - Display names (players are shown as seats and "you"; see Risks).
  - Spectators; more than 2 players.

## Affected areas
- **Core:** `Engine/GameError.cs`, `Engine/GameErrors.cs` → `RuleViolation.cs`, `RuleViolations.cs`. Also `CommandResult`, all action files and tests (rename with the `roslyn-refactor` `rename_symbol` tool; codes unchanged).
- **Domain:**
  - `Entities/Game.cs`
  - `Entities/GamePlayer.cs`
  - `Enums/GamePhase.cs`
  - `ValueObjects/InviteCode.cs`
- **Application.Common:** `Abstractions/IApplicationDbContext.cs` (+ `DbSet<Game> Games`, `DbSet<GamePlayer> GamePlayers`)
- **Application:** `Games/` feature folder:
  - `Contracts/CreateGameContract.cs`
  - `Contracts/JoinGameContract.cs`
  - `Contracts/StartGameContract.cs`
  - `Contracts/GetGameContract.cs`
  - `Contracts/ListMyGamesContract.cs`
  - `Models/GameErrors.cs`
  - `Models/GamePlayerSummary.cs`
- **Infrastructure:**
  - `Persistence/Configurations/GameConfiguration.cs` and `GamePlayerConfiguration.cs`
  - `ApplicationDbContext.cs` (+ DbSets)
  - `Persistence/Migrations/*_AddGames.cs`
- **Api:**
  - `Features/Games/CreateGameEndpoint.cs`, `JoinGameEndpoint.cs`, `StartGameEndpoint.cs`, `GetGameEndpoint.cs`, `ListMyGamesEndpoint.cs`
  - `DependencyInjection.cs` (HTTP JSON options, see step 5)
  - `Govern.Api.http` (sample requests)
- **Reuse:**
  - Aggregate with children through the root: `User` + `RefreshToken` (`Domain/Entities/User.cs`, `UserConfiguration`)
  - Value object with a converter: `Email` + `UserConfiguration`
  - Concurrency token: `RefreshTokenConfiguration` + `RefreshTokenHandler`
  - Unique violation handling: `IDbExceptionClassificationService` + `SignUpHandler`
  - Current user: `ICurrentUserService`
  - Test auth: `UsersApi.CreateSignedInUserAsync`
  - Engine: `GameFactory.Create`, `GameEngine.ViewFor`, `GameSerializer`

## Steps
1. **Core: rename errors.**
   - Rename `GameError` → `RuleViolation` and `GameErrors` → `RuleViolations`. `CommandResult.Error` becomes `CommandResult.Violation`.
   - This frees the name `GameErrors` for the Application feature, following the `[Feature]Errors` convention.
   - Error codes (`move.unreachable`, …) do not change.
   - Update `game-core.md` (feature doc and rule).
2. **Domain.**
   - `GamePhase` enum: `Lobby`, `InProgress`, `Finished`.
   - `InviteCode` value object:
     - 16 random bytes from `RandomNumberGenerator`, base64url, 22 characters.
     - `New()`, `TryCreate(string)` (length and alphabet checked), `Create(string)`.
     - `MaxLength = 22`.
   - `GamePlayer : BaseEntity`: `GameId`, `UserId`, `Seat` (0 or 1), `JoinedAt`. Created only through `Game`.
   - `Game : BaseAuditableEntity`:
     - Properties: `CreatorId`, `InviteCode`, `Phase`, `Revision` (concurrency token, incremented by every mutation), `GameState? State`, and a `Players` backing-field collection.
     - `Game.Create(creatorId, now)`: new invite code; the creator joins at seat 0.
     - `Join(userId, now)`:
       - idempotent for an existing player;
       - throws `InvalidOperationException` when not in the lobby or already full, because the handler checks first;
       - takes seat 1.
     - `Start(userId)`:
       - only the creator, only in the lobby, only with 2 players (all checked by the handler; the domain throws);
       - builds `GameSetup([seat0, seat1], seed)` with a `RandomNumberGenerator` seed that is never exposed;
       - sets `State = GameFactory.Create(setup)` and `Phase = InProgress`.
     - Helpers: `IsPlayer(userId)`, `IsFull`.
3. **Application.Common:** add the `DbSet`s to `IApplicationDbContext`.
4. **Application** (`Games/`). Handlers first check `ICurrentUserService.UserId`; with no user they return `UserErrors.NotAuthenticated`, as `GetCurrentUserHandler` does.

   | Contract | Request → Response | Handler rules |
   |---|---|---|
   | `CreateGame` | `()` → `(Guid Id, string InviteCode)` | Reject when the user already has 5 lobbies they created (`game.too_many_open_lobbies`). The limit is a constant in `GameErrors`' feature |
   | `JoinGame` | `(string InviteCode)` → `(Guid GameId)` | Validator: `InviteCode.TryCreate`. Unknown code → `game.invite_not_found` (404). Existing player → success (idempotent). Not in lobby → `game.already_started` (409). Full → `game.full` (409). A race on seat 1 (unique `(game_id, seat)`) is caught as a unique violation → `game.full`. A `DbUpdateConcurrencyException` on `Revision` → reload once and re-evaluate |
   | `StartGame` | `(Guid GameId)` → `GetGameResponse` | Not a player → `game.not_found` (404, not 403: the game's existence isn't revealed). Player but not creator → `game.not_creator` (403). Not in lobby → `game.already_started` (409). One player → `game.not_enough_players` (409). Concurrency on `Revision` → `game.already_started` |
   | `GetGame` | `(Guid GameId)` → `(Guid Id, GamePhase Phase, bool IsCreator, string? InviteCode, IReadOnlyList<GamePlayerSummary> Players, PlayerView? View, DateTimeOffset CreatedAt)` | Not a player → `game.not_found`. `InviteCode` only while in the lobby. `View = GameEngine.ViewFor(State, viewer)` once started. `GamePlayerSummary(Guid PlayerId, int Seat, bool IsYou, bool IsCreator)`: no emails and no user ids; `PlayerId` is the per-game seat id used in the view |
   | `ListMyGames` | `(int Page = 1, int PageSize = 20)` → `(IReadOnlyList<MyGameItem> Items, int Total)` | Validator: page ≥ 1, page size 1–50. Games the user plays in, newest `UpdatedAt` first. `MyGameItem(Guid Id, GamePhase Phase, int PlayerCount, bool IsCreator, DateTimeOffset UpdatedAt)`, projected in SQL |

   `Games/Models/GameErrors.cs` holds all the codes above. Reads use `AsNoTracking()` plus projections. Only `Start` and `Join` load the tracked aggregate with `Include(g => g.Players)`.
5. **Infrastructure.**
   - `GameConfiguration`:
     - `InviteCode`: converter + max length 22 + unique index.
     - `Phase` stored as a string (max 16).
     - `Revision` has `IsConcurrencyToken()`.
     - `State` is stored as `jsonb`:
       - `HasColumnType("jsonb")`;
       - converter `GameSerializer.Serialize` / `GameSerializer.DeserializeState`;
       - a reference-equality `ValueComparer`: the state is immutable and replaced on every change, so EF must not deep-compare it;
       - nullable while in the lobby.
     - Index on `CreatorId`; `Players` navigation in field access mode, with cascade delete.
   - `GamePlayerConfiguration`:
     - unique `(game_id, user_id)` and `(game_id, seat)`;
     - index on `user_id`;
     - FK to `users` with restrict delete.
   - Migration: `dotnet ef migrations add AddGames …`. Review the `jsonb` column and the indexes.
6. **Api.**
   - Routes, all `.RequireAuthorization()`:

     | Route | Returns | Errors |
     |---|---|---|
     | `POST /api/games` | 201, `Location: /api/games/{id}` | 409 |
     | `POST /api/games/join` | 200 | 400/404/409; the code goes in the body, so it never appears in URLs or server logs |
     | `POST /api/games/{id}/start` | 200 | 403/404/409 |
     | `GET /api/games/{id}` | 200 | 404 |
     | `GET /api/games` | 200 | 400 |

   - HTTP JSON options: add `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)`, so `PlayerView` and `GamePhase` serialize exactly as `GameSerializer` does. The client and SSE (Plan 3) then see one format. Existing responses have no enums; verify that.
7. **Client:** none (Plan 4).

## Tests
- **Unit (Domain, Core):**
  - `InviteCodeTests`: `New` gives 22 base64url characters, unique across 1,000 calls; `TryCreate` rejects empty, wrong length, bad characters and whitespace; `Create(New().Value)` round-trips (codes are case-sensitive and never normalised).
  - `GameTests`:
    - `Create` seats the creator at 0;
    - `Join` seats the second player and is idempotent;
    - `Join` when full or started throws;
    - `Start` by the creator with 2 players creates a state whose players match the seats, and sets `InProgress`;
    - `Start` by a non-creator, with 1 player or twice throws;
    - `Revision` increments on every mutation.
  - Core: the renamed tests still pass; no behaviour change.
- **Integration** (`Features/Games/*EndpointTests.cs`):
  - Create: 201 + `Location` + persisted row with the creator at seat 0; 401; 409 at the lobby limit.
  - Join: 200 + persisted seat 1; idempotent second call; 400 for a malformed code; 401; 404 for an unknown code; 409 for full and for started. **Concurrency:** two users join with `Task.WhenAll`; exactly one gets seat 1 and the other gets `game.full`.
  - Start:
    - 200 with `View.Viewer` = the caller and the state persisted as `jsonb`, read back and deserialized;
    - 401;
    - 403 for a non-creator player;
    - 404 for an outsider;
    - 409 for one player and for already started;
    - **concurrency:** two starts at once, exactly one succeeds.
  - Get:
    - lobby shape (invite code visible, `View` null);
    - started shape (invite code null, `View` hides the opponent's units outside sight);
    - 401; 404 for an outsider;
    - the response JSON of `View` equals `GameSerializer.Serialize(view)`.
  - List: only the caller's games, ordered, paged; 400 for page size 0 and 51; 401.
  - `Security/EndpointAuthorizationTests`: no allow-list change (every new endpoint requires auth).
- **Architecture:** none. Domain → Core and Application → Core are already allowed, and the new files fit the existing templates.

## Docs & rules
- New `docs/features/games.md`: the lobby lifecycle (phases), endpoints with status codes, error codes, the data model (`games`, `game_players`, the `jsonb` state), invite code rules and privacy (no emails exposed).
- Update:
  - `docs/features/game-core.md` and `.claude/rules/game-core.md` (rename to `RuleViolation`/`RuleViolations`)
  - `docs/plans/game-delivery.md` (Plan 2 → Done)
  - `docs/README.md`
- `.claude/rules/persistence.md`: add the `jsonb` snapshot pattern (converter through `GameSerializer`, reference `ValueComparer`, no querying inside the JSON). That's a new convention, and it comes with its integration test.

## Verification
- `dotnet build Govern.slnx`: 0 warnings.
- `dotnet ef migrations add AddGames --project src/Govern.Infrastructure --startup-project src/Govern.Api --output-dir Persistence/Migrations`. Review the migration: `state jsonb NULL`, unique `invite_code`, `(game_id, user_id)` and `(game_id, seat)`.
- `/verify`: build, format, architecture, unit, client and integration tests; the full `dotnet test Govern.slnx` green with the summary shown.
- Manual run with `Govern.Api.http`: sign up two users → create → join with the code → start → `GET /api/games/{id}` as each user shows two different `PlayerView`s.
- `code-reviewer`, then `security-auditor` (new endpoints). Also check invite codes appear in no log line.

## Risks & open questions
- **Core rename (decided: rename):** `RuleViolation`/`RuleViolations` avoids clashing with the Application feature's `GameErrors` and with `Utils.Results.Error`.
- **Display names:** users only have an email, and showing it to whoever holds an invite link would leak personal data. v1 shows seats ("You", "Opponent"). A `DisplayName` on `User` is a separate small plan if you want names.
- **Invite code as a bearer secret:** anyone with the link can take the free seat. That's acceptable for a 2-player MVP: the code is 128 bits, the creator can see who joined, and regenerating it is deferred. The code is never logged, because it's sent in the request body.
- **`jsonb` read cost:** `GetGame` loads and deserializes the whole state (about 20 KB at 20×14). Fine at this scale. `ListMyGames` never touches the `State` column (projection).
- **"Your turn" in the list** needs the current player. Plan 3 adds a denormalised `current_player_id` column, updated with each command, instead of querying inside the JSON.

## Decisions made during implementation
- **`PlayerId` is the per-game `GamePlayer.Id`, never the user id.** `PlayerView` carries player ids (viewer, current player, winner, owners, players), so user ids would otherwise reach the opponent's browser. `GamePlayerSummary` exposes `PlayerId` so the client can map seats to the view. Plan 3 resolves the actor through `Game.FindPlayer(userId).PlayerId`.
- **Core project references:** `Govern.Domain`, `Govern.Application` and `Govern.Infrastructure` now reference `Govern.Core`. The layers already allowed it.
- **One race path for joins:** a `DbUpdateConcurrencyException` on `revision` and a unique violation on `(game_id, seat)` or `(game_id, user_id)` both lead to one re-read in a fresh context. The re-read returns success when the caller is now seated (the same user joined twice at once), and otherwise `game.already_started` or `game.full`.
- **Paging bound:** `page` is also capped (1–10,000), so the row offset can't overflow.
- **`MyGameItem`** lives in `Games/Models`, because contract files may only declare `Xxx{Request,Response,Validator,Handler}`.
- **Creator FK:** `games.creator_id` also gets a restrict FK to `users`, next to its index.
- **Invite code visibility:** only the creator gets the code back from `GET /api/games/{id}`, and only while in the lobby. The joined player doesn't need it, so leaving a lobby later won't leak it.
- **`StartGame` checks membership in SQL**, as `GetGame` does, so an outsider never makes the server load a game's state.
- **Deterministic race tests:** `Support/GameRowLock` holds the `games` row lock until every concurrent request has read the game and is blocked on its write. Start's revision-conflict branch is therefore covered. Join's losers always fail on the unique seat index, because EF inserts the seat before updating `games`; the revision catch stays as a defensive path.

## Follow-ups from the code review and security audit (before stage → main)
- **Medium, mitigated in Plan 3a, still open:** per-user `games`/`game-commands` rate limits and a cap of 20 unfinished games ([games limits](../features/games.md#games-limits)) slow the growth, but finished games are neither capped nor deleted, and `resign` finishes a game at once. Two accounts looping create → join → start → resign still add ~10–13 games (~13 KB state each) per minute, indefinitely (Plan 3a security audit). Remaining fix before stage → main: a rolling per-user quota on create/join (e.g. games created or joined in the last 24 h, finished included), plus a storage alert (follow-ups #5) or storage auto-grow. **Original finding:** the lobby cap counts only `lobby` games, and starting a game frees a slot. A create → join → start loop can therefore write ~20 KB `jsonb` rows without limit, and the Postgres storage (32 GB, auto-grow off) would fill. Fix: a `games` rate-limit policy partitioned by user id on create, join and start (and on the Plan 3 command endpoint), plus a cap on games that are not finished.
- **Low:** the lobby cap is a soft cap, because simultaneous creates can overshoot it.
- **Medium, open (raised by Plan 3a):** lobbies never expire and can't be left or cancelled. With the unfinished-games cap (20, which counts joined lobbies), a user who joins 20 lobbies that are never started is locked out of create and join. Add lobby leave/cancel and expiry of stale lobbies before stage → main.
- **Low, moved to Plan 3b:** `PlayerView.Version` and the shared `NextId` counter reveal how many things the opponent did. Decide before the SSE format is frozen (per-player counters or opaque ids). Plan 3a only asks clients to echo `view.version` as `expectedVersion`, so either fix keeps the command contract.
- **Plan 3, resolved in 3a:** Core `RuleViolations` and Application `GameErrors` share the `game.` prefix without colliding; `GameErrorCodesTests` keeps them disjoint. Command bodies are read with `GameSerializer` (`GameCommandBody`).
- **Plan 4:** put the invite code in a URL fragment (`/join#<code>`), never in the path or query. The client needs its own `GamePhase` model, because it can reference only Core.
