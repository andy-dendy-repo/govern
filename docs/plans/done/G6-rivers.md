---
title: "Plan: G6 rivers (bridges with G10)"
tags: [plan, game, map, rivers, movement, pathfinding, combat, map-generation, ruleset, preview, client]
code: [rulesets/default/v4.json, src/Govern.Core/Map, src/Govern.Core/Engine/CombatCalculator.cs, src/Govern.Core/Visibility, src/Govern.Client/Features/Games]
updated: 2026-10-01
---

# Plan: G6 rivers (bridges with G10)

**Status:** done 2026-10-01 (PR #32). Proposed and approved the same day; the owner decided that deep rivers stay and map generation places no bridges. See [Decisions made during implementation](#decisions-made-during-implementation) for where the result differs from the plan below.

## Context
The map has terrain on tiles only, so every obstacle is a whole tile. G6 adds rivers that run **along hex edges** (as in Civ). Crossing them is free or costly, and the defender gets a bonus against attacks across them. It is the first edge-based rule, so movement, the view previews and map generation learn to look at the step between two tiles, not just the tile entered. The owner decided (2026-10-01) to keep all three depths, including the deep river that only a bridge crosses, but not to have map generation place bridges: bridges come with builders in G10. Until then a deep river is a wall to walk around. Ships (G15) and seasons (G14) don't exist yet either; this plan leaves clean hooks for all three.

## Scope
- In:
  - **River kinds (ruleset `rivers.kinds`, ids like terrain):**

    | Kind | State | Land units crossing | Defender across it gets |
    |---|---|---|---|
    | Stream | 1 | free (+0) | +0 |
    | River | 2 | +1 movement on top of the tile's cost | +3 strength |
    | Deep river | 3 | can't cross (needs a bridge, G10) | +5 strength |

  - **Movement:** the step from tile A to tile B costs B's terrain cost plus the crossing cost of the edge between them; a deep river edge can't be crossed. Rivers don't block sight.
  - **Combat:** a **melee** attack across a river edge gives the defender that kind's `defenceBonus` (a new `River` combat modifier, shown in the forecast like terrain). Ranged attacks ignore rivers. Melee attacks across a deep river are allowed (the attacker stays put, as today).
  - **State:** each tile owns the river edges on its east, north-east and north-west sides (`Tile.Rivers`: side, kind), so every edge is stored once, by one tile. Left out of the JSON while empty, so stored games read unchanged (no schema bump) and simply have no rivers.
  - **View:** `TileView.Rivers` carries the same list for explored tiles (rivers are static, like terrain). No coordinates are added to the view. An edge owned by a still unexplored tile shows once that tile is explored; previews never need it, since every tile a unit can reach is visible.
  - **Map generation:** `rulesets.map.generation.rivers` (count, minimum and maximum length, the segment at which each kind starts). Each river starts at a corner touching hills or mountains, walks along connected edges (consecutive edges share a corner) away from its source, and ends at a lake or the map edge. It starts as a stream and deepens downstream. Every edge is written with its mirror, so both halves stay the same; a segment whose mirror isn't an edge (odd map heights, between rows) ends the river there. Spawn tiles don't block rivers. The connectivity check that already guards impassable tiles also counts deep edges: a deep segment that would cut the passable map in two is laid as a river instead, so every passable tile stays reachable.
  - **Ruleset v4:** `rulesets/default/v4.json` (v3 untouched) with the kinds, the generation numbers and texts (`river.stream.name`, `river.river.name`, `river.deep_river.name`). The Api and client embed v4.
  - **Client:** the reserved `BoardLayer.Rivers` layer draws each river edge as a line on the hex border, wider and darker per kind; the movement-cost lens marks river crossings (+1, ✕); the tile tooltip and unit panel say "River: +1 to cross" or "Deep river: can't cross"; the combat forecast lists the river modifier; a `/design` gallery entry for the three kinds.
- Out (deferred):
  - **Bridges (owner, 2026-10-01: no generated bridges):** G10, where builders build, pillage and repair them. G10 adds the bridge flag on `RiverEdge` and makes the step check accept a bridged deep edge; G6 keeps all crossing behind that one step check.
  - Ships sailing along rivers, and whether they pass under bridges: G15. No `navigable` field until then.
  - Seasons changing a river's state (frozen, flooding): G14.
  - Fresh water and housing from rivers: G5 and G8.
  - A sea: there is none yet, so rivers end at lakes or the map edge.

## Affected areas
- Ruleset: `rulesets/default/v4.json` (new; v1 to v3 untouched).
- Core:
  - `Map/RiverType.cs` (id struct + JSON converter, like `TerrainType`), `Map/RiverDefinition.cs` (crossing cost, crossable, defence bonus), `Map/RiverEdge.cs` (side, kind), `Map/HexSide.cs` (east, north-east, north-west), `Map/Tile.cs` (+ `Rivers`), `Map/GameMap.cs` (`RiverBetween(a, b)`, `StepCost`, `CanStep`).
  - `Map/ReachabilityCalculator.cs`: the callbacks take the step (from, to) instead of the tile.
  - `Map/MapGenerator.cs` (rivers after terrain patches; connectivity counts deep edges).
  - `Rulesets/RiverRules.cs`, `Rulesets/RiverGenerationRules.cs`, `Rulesets/Ruleset.cs`, `Rulesets/RulesetDocument.cs`, `Rulesets/MapGenerationRules.cs`, `Rulesets/RulesetValidator.cs`.
  - `Engine/Combatant.cs` (+ the river kind crossed, if any), `Engine/CombatCalculator.cs` (river modifier), `Visibility/CombatModifierKind.cs` (+ `River`).
  - `Visibility/TileView.cs` (+ `Rivers`), `Visibility/VisibilityCalculator.cs`, `Visibility/ViewPreviewCalculator.cs` (step-based `CanEnter` and cost, river in `CombatForecast`).
  - `Actions/MoveUnitAction.cs` and `Actions/AttackAction.cs` pick up the step cost and the river modifier through the shared calculators.
  - Reuse: `GameMap.MirrorOf`, `MapGenerator.Layout.PassableTilesAreConnected`, `DeterministicRandom.NextHashed`, `GameStateBuilder` (+ `WithRiver(a, b, kind)`), `TestRulesets.With`.
- Application / client: embed v4 in `Govern.Application.csproj` + `Games/Services/DefaultRulesetService.cs`, `Govern.Client.csproj` + `Features/Games/Services/RulesetService.cs`.
- Client:
  - `Features/Games/Components/Board/BoardRivers.razor` (new layer), `Features/Games/Helpers/HexLayout.cs` (edge end points), `Features/Games/Components/Board/GameBoard.razor`.
  - `Features/Games/Lenses/MovementCostLens.cs` (crossing marks), `Features/Games/Components/UnitPanel.razor`, `Features/Games/Components/CombatSideView.razor` (river modifier label).
  - `Features/Design/Pages/DesignPage.razor` (gallery entry).

## Steps
1. Core, ruleset data: `rivers.kinds` and `map.generation.rivers` records; validator checks with JSON paths: unique kind ids, `crossingCost` ≥ 0, `defenceBonus` ≥ 0, at least one crossable kind, the first generated kind crossable, a text per kind, generation lengths `1 ≤ min ≤ max`, kind start segments ascending and naming known kinds.
2. Core, `rulesets/default/v4.json`: v3 plus the kinds above, generation numbers (proposed: 2 rivers per half, length 4 to 12, river from segment 3, deep river from segment 7) and texts.
3. Core, map model: `HexSide`, `RiverEdge`, `Tile.Rivers`, `GameMap.RiverBetween`, step cost and step check; JSON omits empty lists.
4. Core, movement: `ReachabilityCalculator` callbacks become step-based; `MoveUnitRule` and the preview share them.
5. Core, combat: `Combatant` carries the river crossed by a melee attack; `CombatCalculator` adds the `River` modifier for the defender.
6. Core, map generation: river walk, mirroring, deepening, deep segments downgraded where they would split the map; re-record `GameFactoryTests.Create_KnownSeed_GivesTheRecordedState`.
7. Core, view: `TileView.Rivers` for explored tiles; previews use rivers from the view.
8. Application and client: embed v4.
9. Client: rivers layer, lens marks, panel and forecast texts, gallery entry.

## Tests
- Unit (Core):
  - `RulesetTests`: the new sections parse; each validator check fails with its path.
  - `GameMapTests`: `RiverBetween` finds an edge from either side and for all six directions; step cost per kind; a deep edge can't be stepped over from either side.
  - `MoveUnitActionTests`: crossing a stream costs the tile only; crossing a river costs +1 and is refused when movement is short; a deep river is refused; the path goes around a river when that is cheaper.
  - `CombatCalculatorTests` / `CombatForecastTests`: melee across each kind, deep river included, gives the defender that kind's bonus; ranged doesn't; `Execute` hits exactly the forecast's lowest and highest damage for the new fights over many seeds.
  - `GameEnginePreviewTests`: parity of `ReachableTiles` and `AttackTargets` with every tile near the unit, in new scenarios with a stream, a river and a deep river.
  - `MapGeneratorTests`: rivers are mirrored; each starts next to hills or mountains and ends at a lake or the edge; edges are connected; kinds only deepen downstream; the passable map stays connected across deep edges; spawn tiles stay reachable; the same seed gives the same rivers.
  - `VisibilityCalculatorTests`: rivers show on explored tiles only; `GameSerializerTests`: a v3-era snapshot without rivers reads unchanged, and a tile with rivers round-trips.
- Integration: none new (no endpoint change); existing game tests run on v4.
- Client (bUnit): `BoardRivers` draws one line per edge with the kind's class; `MovementCostLens` marks crossings; `UnitPanel` and `CombatSideView` show the river texts; gallery renders.
- Architecture: none new; `RulesReferenceTests` gains a "River kinds" table checked against v4.

## Docs & rules
- `docs/game/rules.md`: a "Rivers" section with the river kinds table (crossing, defence bonus).
- `docs/game/game-data.md` (`rivers` and `map.generation.rivers`, tile `rivers`, v4; drop G6 from "Future sections"), `docs/game/game-logic.md` (step cost, river defence, river generation), `docs/features/game-core.md`, `docs/features/game-ui.md` (rivers layer, lens marks), `docs/game/design-roadmap.md` (G6 what shipped; bridges move into G10; G14 and G15 hooks), `docs/README.md`.
- `.claude/rules/game-core.md`: movement is per step (edge cost), the previews' parity scenarios include rivers, and generated rivers are mirrored edge by edge.
- Close-out in this PR: plan to `docs/plans/done/`, backlog row to "Recently done".

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green, summary shown.
- Run the Api and client against a Postgres container, play both seats: check rivers on the board in light and dark and at phone width, the movement range and the lens around a stream, a river and a deep river, and the river bonus in the combat forecast. Screenshots in `/mnt/project-files/screenshots/g6-rivers/`.

## Risks & open questions
- **Deep rivers without bridges** can make long detours until G10. The generator never lets them split the map, so a way round always exists.
- **Crossing cost:** +1 movement is proposed instead of Civ's "uses all remaining movement", because it is easier to read and lets the horseman cross and still step once. It is a ruleset number, so either can be tuned later.
- **Odd map heights:** the 180° mirror keeps tiles in the same row adjacent but not tiles in neighbouring rows, so rivers on odd-height maps are shorter. Accepted; the default map is 20 × 14.
- **Size:** about the size of G4 (model, generator, previews, client layer). The step-based movement change touches every movement test helper, so it lands first and on its own commit.

## Decisions made during implementation
- **Names:** the river id is `RiverType` (like `TerrainType` and `UnitType`), not `RiverKind`. The kinds are a top-level `rivers` list in the ruleset; generation reads its source and mouth terrains from `map.generation.rivers.sources` and `mouths` instead of hard-coding hills, mountains and lake.
- **Tile model:** a tile stores its three owned edges as one `TileRivers` record (an optional `RiverType` per side), not a list. It compares by value, and JSON leaves out both the sides without a river and the whole field on a tile without rivers.
- **Capturing across a deep river** is refused (`attack.river_in_the_way`), because capturing moves the attacker into the city. Fighting a unit across one is allowed.
- **Where river rules show:** the board's river lines carry a `<title>` sentence, the movement-cost lens adds a badge on each costly or blocked river edge (`IMapLens.EdgeMarksFor`), and the forecast lists "+3 behind River". The unit panel line from the plan was left out: the panel describes the unit, not the tiles around it.
- **Rivers layer** renders only when the view holds a known river, like the lens layer only when a lens is on.
- **Generation numbers:** 2 rivers (each with its mirror) of 4 to 12 edges, river from edge 3, deep river from edge 7. Over the first 12 seeds of the default map every map has 4 rivers and most have a deep stretch.
- **Tests added beyond the plan:** `HexEdge` ownership from both sides, a deep river on one edge (walk round) and on every near edge (unreachable), river systems that start next to a source and end at a mouth, and `GameUiTests.BoardCss_StylesEveryRiver`.
