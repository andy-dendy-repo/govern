---
name: game-core-checks
description: Govern.Core game engine review checklist - fog-of-war oracles via error codes/ids, sight vs movement invariant, STJ polymorphic exceptions, gamecore branch CI gap
metadata:
  type: project
---

Recurring risks found in the first Govern.Core review (2026-09-27, Plan 1 on feature/game-core-engine):
- **Fog oracles through rejected commands:** rejected commands never change state, so a client can probe freely. `MoveUnitRule` returned `move.occupied` for hidden enemy units anywhere on the map; `unit.not_owned`/`city.not_owned` vs `*.not_found` enumerate enemy ids. Every new rule/error must be checked for "does the error differ based on something the actor cannot see?"
- **Sight vs reach invariant:** pathfinding blocked by units and `City.MinimumSpacing` checks read full state; they are only leak-free while Movement (at min terrain cost) <= Sight and MinimumSpacing - 1 <= settler Sight. Flag new units/terrain that break it.
- **Client preview gap:** `GameEngine.ReachableTiles` takes `GameState`, but the client only gets `PlayerView` (Plan 5 relies on it).
- **STJ:** missing `type` discriminator on an abstract polymorphic base throws `NotSupportedException`, not `JsonException` (500 in minimal-API binding). File-based `dotnet run` apps default to AOT with reflection JSON disabled; add `#:property PublishAot=false` when probing.
- **Branch flow:** game work PRs target `gamecore`; ci.yml and bash-guard only know stage/main.

Fixed in the same PR: both oracles, ReachableTiles viewer scoping, JsonException wrapping, schema pre-check, PQ tie-break, CI trigger for gamecore, purity arch tests. Still open: PlayerView-based preview overload (Plan 5), bash-guard does not protect gamecore.

**Why:** hidden information is the core anti-cheat promise of ADR 0004; these leaks pass all unit tests.
**How to apply:** on any Core change, re-run the oracle check per error code, and on Plans 2-5 check the Api maps errors without adding detail and never sends GameState/raw events. See [[convention-sweep-gaps]].

Plan 2 review (2026-09-27, feature/game-lobby: Game/GamePlayer, jsonb state, 5 /api/games endpoints). No critical findings. Checks worth repeating on Plans 3-5:
- `PlayerId` = `GamePlayer.Id` (never a user id); every response with a `PlayerView` must stay free of user ids (`Get_Started_ResponseContainsNoUserIds` pattern).
- Endpoints whose request has a Guid `NotEmpty` validator return 400 but tended to miss `.ProducesValidationProblem()` and `<response code="400">` (Get/Start). Check every new route-id endpoint.
- Race handling via `Revision` concurrency token + unique indexes is correct, but `Task.WhenAll` tests don't deterministically hit the catch branches; ask for a forced race if a new catch path is added.
- jsonb reorders object keys, so anything deserialized from `games.state` needs `AllowOutOfOrderMetadataProperties` (GameSerializer has it; HTTP JSON options do not, so Plan 3 command bodies must go through `GameSerializer.Options`).
- Core `RuleViolations` and Application `GameErrors` share the `game.` code prefix; watch for collisions when Plan 3 maps violations to `Error`.

Plan 3a review (2026-09-27, feature/game-commands: command endpoint, per-user rate limits, unfinished-games cap). No critical findings. Verified, don't re-derive:
- `GameRowLock` makes the command race deterministic: reads aren't blocked, UPDATE ... WHERE revision blocks, EPQ re-check fails -> DbUpdateConcurrencyException -> `game.stale_version`.
- `MapInboundClaims = false` and UseRateLimiter comes after UseAuthorization, so per-user (`sub`) partitions work; the IP fallback in `PerUser` is unreachable.
- `GameSerializer` has `RespectRequiredConstructorParameters` and throws on `null`, so empty/`null`/missing-field bodies are 400 from `GameCommandBody.BindAsync`.
Checks worth repeating: any per-user cap must have a way out for every counted state (joined lobbies can't be left, resigned or expire, so the unfinished-games cap can lock a user out); new 409 codes must also go into the endpoint table rows of games.md, not only the error-code table; a shared rate-limit policy test must hit every endpoint it claims (Join was unasserted); the size cap has two branches (Content-Length and streamed) and each needs a test.

Plan 3b review (2026-09-28, feature/game-live-updates: SSE stream, opaque view version, random ids). Critical found:
- **RNG outputs must never reach clients.** Ids drawn with `DeterministicRandom.Next(1, int.MaxValue)` expose ~31 bits of a SplitMix64 output; the mix is invertible, so two consecutive own ids recover `RngState` by brute-forcing the 2^33 quotient (verified: ~3 s, 8 threads, clang). That reveals the enemy spawn tile (drawn right after P0's ids) and every future id (index in the stream = opponent's creation count). Fix pattern: pass the 64-bit draw through SHA-256 (or a keyed hash) before truncating it to an id.
- `MyGameItem.UpdatedAt` (and the list order by it) changes on hidden commands, so polling `GET /api/games` leaks when and how often the opponent acts. It was there before 3b, but it contradicts the "timing doesn't leak" claims in ADR 0005.
Verified OK, don't re-derive: events dispatch in `SavedChangesAsync` with no explicit transactions (post-commit, so subscribe-before-read is sound); .NET `ChainedRateLimiter.IdleDuration` is null if any child is busy, else the minimum; `ToServerSentEvents` cancels then drains a pending MoveNext before `DisposeAsync`.
**How to apply:** for any id, token or value derived from game randomness that a client sees, ask "how many bits of raw PRNG output does this expose?". For any timestamp or counter on a game row that a client sees, ask "does a hidden action change it?".

Plan 3c review (2026-09-28, feature/game-lobby-lifecycle: leave, cancel, expiry sweep, seat quota, activity_at). No critical findings. Medium: games can now be deleted (cancel, sweep), so a join that loaded the lobby before the delete inserts a `game_players` row whose FK check waits on the delete and then fails with 23503. That is a plain `DbUpdateException`, not a concurrency or unique-violation error, so it becomes a 500. `IDbExceptionClassificationService` only knew unique violations.
**How to apply:** whenever a change adds a path that deletes a parent row (games, users), check every concurrent child insert (seats, and later outbox, messages) for FK-violation handling, and ask for a GameRowLock race test with the delete as the winner. The `GameRowLock` races with `Task.WhenAll` don't pin which writer wins, so they don't guarantee that the reevaluation branch runs. To force the order, fire the writers one at a time and wait for N lock waiters between them. Verified OK, don't re-derive: `ActivityAt` moves only on Phase/CurrentPlayerId changes, and the sweep's repeated predicate plus EPQ keeps a lobby that was just joined.

Plan 5a review (2026-09-29, feature/core-player-view: TileView without coord, CityView, view-based previews). No production bug; refactored AttackRule/FoundCityRule/CommandGuardRule keep the original error order exactly. Verified, don't re-derive: no unit stacking (move/spawn/capture all require an empty tile), so `UnitAt` order can't differ between state and view; view Dijkstra equals state Dijkstra because every tile within budget is within the mover's own sight (plains only, cost 1); `PlayerSummary` is a class so `FirstOrDefault(...)?.Status` is null-safe.
Checks worth repeating:
- **Vacuous Shouldly chains:** `x?.Code.ShouldBe(expected)` skips the assertion when `x` is null. Ask for `(x?.Code).ShouldBe(expected)`. Found in GameEnginePreviewTests and GameEventsReader.
- Parity theories need scenarios on each branch where state and view differ or the shared check branches: own city inside reach, attack on own city (`cityOwner` branch), a hidden enemy at sight + 1, settler with 0 movement. New terrain (cost > 1 or impassable) must add parity scenarios.
- Remaining view leaks (pre-existing, not filtered by CityView): enemy `Unit.MovementLeft`, and city names `City N` = founder's city count at founding (reveals hidden enemy cities).
