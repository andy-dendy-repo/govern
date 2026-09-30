---
name: project-game-core
description: Govern.Core game engine security facts, fog-of-war invariants and open findings from the 2026-09-27 audit (feature/game-core-engine, no endpoints yet)
metadata:
  type: project
---

Govern.Core = pure game engine (BCL only), shipped to the WASM client and used by the Api to validate commands. First audit 2026-09-27 on uncommitted feature/game-core-engine: no Critical/High (no endpoint yet); arch guardrails strengthened (Layers.Ignored removed, Core checks added).

Security model (docs/decisions/0004-game-engine.md, .claude/rules/game-core.md): clients receive only PlayerView (ViewFor); raw GameEvents must be filtered per player; actor comes from the server (the caller's per-game GamePlayer.Id, never a user id), commands carry no player field. Planned (docs/plans/done-game-delivery.md step 3): command endpoint with ExpectedVersion -> 409, SSE pushing PlayerView.

Open at that audit (verified empirically with a scratch probe):
- Medium: MoveUnitRule checks state.UnitAt(target) before reachability -> `move.occupied` vs `move.unreachable` for ANY map tile = fog-of-war oracle (one sweep of rejected moves found every hidden enemy unit). Becomes High once the command endpoint returns RuleViolation codes (renamed from GameError in Plan 2).
- Low: id oracle (unit.not_found vs unit.not_owned, city.not_found vs city.not_owned) over the shared sequential NextId; "City {Cities.Length+1}" names leak total city count.
- Low: GameEngine.ReachableTiles(state, unitId) has no viewer/owner check (returns an enemy unit's range).
- Low: GameSerializer.Deserialize<GameCommand> without a `type` throws NotSupportedException (not JsonException as documented) -> 500 in minimal-API binding.
- Low: no Core helper to filter GameEvents per viewer; PlayerView exposes enemy City.Producing/ProductionProgress and Unit.MovementLeft on visible tiles, and Version (opponent command count).
- Low: comma-separated enum strings ("settler, soldier") and case-insensitive names deserialize (flag OR); SetProductionRule's Enum.IsDefined is the real guard.

Resolved in the same PR before commit: the move.occupied oracle (occupied only for visible targets), the id oracle (enemy ids -> *.not_found; not_owned codes removed), per-owner city names, ReachableTiles(state, viewer, unitId), missing discriminator -> JsonException; catalog invariants now tested (UnitCatalogTests). Still open: event filtering helper, enemy production/MovementLeft/Version visibility in PlayerView, enum-flag strings (guarded by IsDefined).

Fog invariants that currently hold (re-check when unit/terrain/city stats change): unit Movement <= Sight and terrain MovementCost >= 1 (pathing never crosses fog); City.MinimumSpacing - 1 <= settler Sight (city.too_close only reflects visible cities); attack range 1 <= Sight. Seed/RngState must never reach the client (spawns are seed-deterministic); host must pick the seed with a CSPRNG.

**Why:** these are latent until game-delivery step 3 (command endpoint + SSE) ships.
**How to apply:** at the audit of step 3, re-verify each open item and treat the move.occupied oracle as High if still present; check event filtering and the command body size limit.

Re-verified 2026-09-27 at the Plan 3a command-endpoint audit (feature/game-commands): all rejections reachable over HTTP respect fog (move.occupied only for visible targets; unit/city not_found for enemy ids; attack adjacency and city.too_close within sight; Movement 2 <= Sight 2, plains cost 1). No command input found that throws (Map.Contains checked before DistanceTo; no checked arithmetic). Events are not returned yet.

Plan 3b audit 2026-09-28 (uncommitted feature/game-live-updates, base origin/gamecore):
- HIGH, open at audit: unit/city ids became raw draws of the gameplay SplitMix64 (`random.Next(1, int.MaxValue)`, 31 of 64 output bits). Two consecutive own ids recover the state in ~2 s on a 12-core laptop (2^33 candidates, SplitMix64 mix is invertible; ~4 candidates, disambiguate by own spawn); state - k*gamma = seed. Gives enemy spawn tiles, exact count of hidden enemy creations (step gamma until own next id matches) and all future draws. Probe: C brute force in scratchpad (not kept). Proposed fix: ids from a keyed PRF (HMAC-SHA256(IdKey 32 CSPRNG bytes, counter)) separate from gameplay RNG; never expose SplitMix64 outputs.
- Low: PlayerView lists units/cities in state (creation) order -> relative creation order of visible enemy objects; sort by id in ViewFor.
- PlayerView.Version is now SHA-256 of the view JSON (viewer-visible data only): not an oracle; stale check compares against caller's own view.

**Why:** RNG outputs visible to clients break the "Seed/RngState never reach the client" invariant.
**How to apply:** at every Core change, check that no client-visible value is a direct output of DeterministicRandom.
