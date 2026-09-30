---
title: "Plan: G4 terrain and the movement-cost lens"
tags: [plan, game, terrain, forest, hills, mountains, lake, map-generation, sight-bonus, ruleset, lens, client]
code: [rulesets/default/v2.json, src/Govern.Core/Map/MapGenerator.cs, src/Govern.Client/Features/Games/Lenses]
updated: 2026-09-30
---

# Plan: G4 terrain (forest, hills, mountains, lake) and the movement-cost lens

**Status:** done 2026-09-30, PR #28. Approved by Andrii 2026-09-30 with the defaults proposed below (one flat terrain list, a new ruleset version, defence and sight-blocking forests deferred).

## Context
Every game is played on an all-plains map, so movement is uniform and the map has no shape. G4 is the second item of content slice C1: a small first set of terrain that changes play (cost to move, blocked tiles, a sight bonus), a seeded map generator that is fair for both players, and the first map lens (movement cost), which G1 moved here because a lens needs terrain that differs. G9 (units and combat) builds on it. Kept lean: only terrain with a mechanic today, only the mechanics nothing else blocks.

## Scope
- In:
  - **Terrains (one flat list, as today):** plains (cost 1), forest (cost 2), hills (cost 2, +1 sight for a unit or city standing on it), mountains (impassable), lake (impassable).
  - **New terrain stat:** `sightBonus` (int ≥ 0).
  - **Map generation:** seeded patches of each terrain, point-mirrored so both halves are the same; both spawns on plains; every passable tile reachable from both spawns. (Spawns stay independent; see "Decisions made during implementation".)
  - **Ruleset v2:** `rulesets/default/v2.json` (v1 stays unchanged, per "never edit a published version"); Api and client embed v2.
  - **Client:** colours, sprite symbols and a board mark for the new terrains; `IMapLens` registry, `LensToolbar` in the map zone, active lens in `GameUiService`, `MovementCostLens` drawn in the `BoardLayer.Lens` slot; the unit panel's Sight shows the hills bonus in its tooltip.
- Out (deferred until a mechanic needs it):
  - **Layered tiles** (base + elevation + feature, e.g. forested hills). Revisit with G5 yields, where combinations first matter. Nothing is shipped, so changing the tile shape then costs only a schema bump.
  - **Defence bonus** (`defenceBonusPerMille`): combat is "attacker always wins" until G9, so it would do nothing. G9 adds it.
  - **Forest blocking sight:** needs line of sight and breaks the fog invariant the previews rely on (a unit must see every tile it can reach). Recorded as an open item on G4 in the design roadmap.
  - Base terrains with no mechanic yet (grassland, desert, tundra, snow), seas, coasts, continents and climate bands: with G5 yields and G15 ships.
  - A tile info panel for an empty tile; other lenses.

## Affected areas
- Ruleset: `rulesets/default/v2.json` (new; v1 untouched).
- Core:
  - `Map/TerrainDefinition.cs` (+ `SightBonus`), `Map/MapGenerator.cs` (new, internal), `Map/GameMap.cs` (mirror helper if needed).
  - `Rulesets/MapRules.cs` + new `Rulesets/MapGenerationRules.cs` (patch list), `Rulesets/RulesetParser.cs`, `Rulesets/RulesetValidator.cs`.
  - `State/GameFactory.cs` (generate the map, mirrored spawns).
  - `Visibility/VisibilityCalculator.cs` (sight + the tile's `SightBonus`).
  - Reuse: `DeterministicRandom`, `ReachabilityCalculator` (already costs per terrain), `ViewPreviewCalculator` (unchanged).
- Application / Client: embedded resource and `ResourceName` in `Application/Games/Services/DefaultRulesetService.cs`, `Client/Features/Games/Services/RulesetService.cs` and both csproj files.
- Client:
  - `wwwroot/css/tokens.css`, `wwwroot/css/board.css`, `Components/Game/GameSprite.razor` (per terrain, as `GameUiTests`/`GameSpriteTests` require).
  - `Features/Games/Components/Board/BoardTerrain.razor` (terrain mark on the tile, so terrain isn't told by colour alone).
  - `Features/Games/Lenses/IMapLens.cs`, `MovementCostLens.cs` (new folder, whitelisted); `Features/Games/Components/Board/LensToolbar.razor`, `BoardLens.razor`; `Features/Games/Services/GameUiService.cs` (active lens); `DependencyInjection.cs` (registry).
  - `Features/Games/Components/UnitPanel.razor` (Sight tooltip with the bonus).
  - `Features/Design/Pages/DesignPage.razor` needed no change: it already lists every ruleset terrain.

## Steps
1. Core, ruleset data: `SightBonus` on `TerrainDefinition`; `map.generation.patches` (`terrain`, `sharePerMille` = target share of tiles, `patchSize`) on `MapRules`; strict parsing (reflection-based, so `RulesetParser` needed no change); validator checks: `sightBonus` ≥ 0, patch terrain exists and is not the default terrain, shares add up to at most 500‰, `patchSize` ≥ 1, impassable share at most 150‰ (so a connected map is always reachable). Fog invariants unchanged (the cheapest passable cost stays plains' 1; a bonus only adds sight).
2. Core, `rulesets/default/v2.json`: the five terrains, their texts, generation shares (forest 150‰, hills 100‰, mountains 40‰, lake 40‰; sizes 4, 3, 2, 3), `defaultTerrain: plains`.
3. Core, `MapGenerator`: pick patch seeds from the game's `DeterministicRandom` anywhere on the map (a seed and its mirror give the same result), grow each patch over random neighbours, and write each tile and its point mirror `(W−1−c, H−1−r)` together. An impassable tile is placed only if all passable tiles stay connected (flood fill). Spawn tiles and their starting-unit rows stay plains. Order-dependent loops sort explicitly.
4. Core, `GameFactory`: pick the spawns as before, then generate the map around them. The known-seed test gets new recorded hashes.
5. Core, `VisibilityCalculator.VisibleTiles`: unit and city sight + `SightBonus` of the tile they stand on.
6. Application and client: embed `v2.json` instead of `v1.json`.
7. Client, terrain look: tokens (light and dark), `board.css` rules, sprite symbols, a small terrain mark drawn on each visible or explored tile.
8. Client, lenses: `IMapLens` (id, name, icon, per-tile label and class from a `TileView` and the ruleset); registry from DI; `GameUiService.ActiveLens` + `ToggleLens`; `LensToolbar` (toggle buttons, one active, keyboard reachable) in the map zone; `BoardLens` renders the active lens in `BoardLayer.Lens`. `MovementCostLens` labels explored and visible tiles with their cost, and ✕ for impassable; unexplored tiles get nothing.
9. Client, `UnitPanel`: Sight shows the effective value; the tooltip explains the hills bonus.

## Tests
- Unit (Core):
  - `RulesetTests`: new fields parse; each validator check fails with its path (negative bonus, unknown or default patch terrain, shares over the limits, size 0).
  - `MapGeneratorTests`: same seed → same map; the map equals its point mirror; spawn tiles are plains; all passable tiles are connected; shares land near the ruleset's; works at 8×8 and 40×40.
  - `GameFactoryTests`: known-seed hashes re-recorded; `MapGeneratorTests` checks spawns on plains.
  - `VisibilityCalculatorTests`: a unit or city on hills sees one tile further.
  - `GameEnginePreviewTests`: parity scenarios with forest (cost 2), and mountains/lake (impassable) around the unit, including a unit with 1 movement left facing forest.
  - `MoveUnitActionTests`: forest costs 2; mountains and lake are rejected with the existing unreachable code.
- Integration: none new (no endpoint change); the existing game tests run on the v2 ruleset.
- Client (bUnit): `GameUiService` lens toggle; `LensToolbar` renders one button per lens, toggles, `aria-pressed`; `BoardLens` draws nothing with no lens and a label per explored tile with the movement lens (✕ on impassable, nothing on unexplored); `UnitPanel` sight with the bonus; sprite/tokens tests cover the new terrains automatically.
- Architecture: `Features/Games/Lenses` whitelisted, holding only `IMapLens` and `XxxLens` types; `RulesReferenceTests` reads v2 and gains the sight-bonus column.

## Docs & rules
- `docs/game/rules.md`: terrain table (movement cost, passable, sight bonus), the map section (generated, mirrored), and the "not enough movement for a cost-2 tile" rule.
- `docs/game/game-data.md` (terrain fields, `map.generation`, v2), `docs/game/game-logic.md` (generation algorithm, sight bonus), `docs/features/game-core.md`, `docs/features/game-ui.md` (lens toolbar built; how to add a lens), `docs/game/design-roadmap.md` (G4 what shipped, deferred parts), `docs/plans/backlog.md`, `docs/README.md`.
- `.claude/rules/game-core.md` (parity scenarios now include terrain), `.claude/rules/game-ui.md` (where a lens goes).

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green, summary shown.
- Run the Api + client against a Postgres container, create and join a game, and check in the browser (light and dark, phone width): the terrains show, moves around forest and mountains match the highlights, the lens toggles; screenshots in `/mnt/project-files/screenshots/g4-terrain/`.

## Risks & open questions
- **Odd map heights:** the point mirror is an exact hex symmetry only when the height is even (the default is 14). For odd heights the halves are the same tile for tile but adjacency along the middle row differs slightly. Proposal: accept it (fairness is still close); alternative is to require even heights.
- **Cost-2 tiles with 1 movement left** can't be entered (today's rule: a move may not cost more than the movement left). Proposal: keep it and state it in the rules; Civ's "always one tile" rule can come with G9 if play needs it.
- **Existing dev games** keep working: their maps are all plains, which v2 still has.

## Decisions made during implementation

- **Spawns are not mirrored.** Mirroring player 2's spawn would tell each player exactly where the opponent started (their own spawn, turned around the centre), which the fog of war is meant to hide. The terrain is still mirrored; each player's starting tiles and their mirrors are kept plains, so no spawn stands on bad ground. That narrows the opponent's possible start spots by about half (measured in review), a small hint that is accepted. Fairness of the start spot is as before G4 (same zone rules).
- **The mirror reveals terrain:** exploring your own half tells you the terrain of the opponent's half. Accepted as the price of a fair map (open to the owner's veto).
- **Hashed draws (from code review):** the generator uses `DeterministicRandom.NextHashed` (SHA-256 of each raw draw). With raw `Next`, the visible map would expose the low bits of many raw outputs, enough in principle to rebuild `RngState` and with it the opponent's spawn and future ids.
- **Generator speed:** the connectivity check reuses its buffers and caches passability per tile; a 40×40 map generates in about 15 ms (Release), once per game.
- **The map is generated after the spawns** from the same `DeterministicRandom`, so unit positions and ids for a seed are unchanged; only the map differs.
- **`rulesets/default/v1.json` no longer parses** with today's Core (`sightBonus` and `map.generation` are required, and parsing is strict). It is kept unchanged as the published record; `docs/game/game-data.md` ("Versions in the repository") describes the rule: new content is a new version file, and a new version keeps every id of the old one while games aren't pinned.
- **Terrain symbols on the board:** each non-default terrain's sprite symbol (hex + a small drawing) is drawn on its tiles, so terrain isn't told by colour alone.
- **Unit panel:** Sight shows the effective value; its tooltip names the hills bonus.
