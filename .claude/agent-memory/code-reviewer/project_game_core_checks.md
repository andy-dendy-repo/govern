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
