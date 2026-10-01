---
title: "Plan: G10a builders, tile improvements, city buildings and bridges"
tags: [plan, game, builders, buildings, improvements, bridges, resources, cities, ruleset, preview, client]
code: [rulesets/default/v5.json, src/Govern.Core/Actions, src/Govern.Core/Cities, src/Govern.Core/Map, src/Govern.Core/Units, src/Govern.Core/Visibility, src/Govern.Client/Features/Games]
updated: 2026-10-01
---

# Plan: G10a builders, tile improvements, city buildings and bridges

**Status:** done 2026-10-01 (PR #33). Proposed and revised the same day after the owner's feedback (no builder charges; builders and soldiers build with their own production, plus the city's inside its area; soldiers build bridges too), then approved ("Lets implement"). G10 is split: **G10a** (this plan) and **G10b** (upgrades, building health, pillage and repair, removing a building), so each PR is a playable increment. See [Decisions made during implementation](#decisions-made-during-implementation) for where the result differs from the plan below.

## Context
Andrii asked (2026-10-01) for a builder, utility buildings on tiles (farm, mine, …) and one-per-city buildings (army camp, campus, trade post, culture centre, harbour) that each give a resource such as science or culture for now. Their later mechanics (traders from trade posts, faster army production in camps, ships from harbours) come later. The roadmap's G10 also owns bridges since G6 (merged, PR #32) left deep rivers as walls. G10 depends on G7 territory, G5 yields and G8 city stats, none of which exist, so this plan uses two simple stand-ins: a city's **work area** is the ring of tiles next to it, and resources go into a **per-player stockpile** that is only shown for now.

## Scope
- In:
  - **Building with units (owner's model, 2026-10-01):** construction takes production, not charges, and the unit is never used up.
    - Units have their own **build production** per turn, a ruleset number: builder 2, soldier 1. Units without it can't build.
    - **Builders** build improvements and bridges (ability `build`); **soldiers** build bridges only (ability `build_bridge`).
    - A unit starts a job with a build command on its tile (an improvement) or on a river edge of its tile (a bridge). The job is stored on the unit with its progress. At the end of the owner's turn the job gains the unit's build production, and **inside a city's work area also that city's production**, which the city then doesn't spend on its own item that turn. When the progress reaches the cost, the improvement or bridge appears and the unit is free again.
    - **Outside every work area** a unit builds with its own production only, and only bridges (improvements need a city).
    - One city's production feeds one job per turn: when several of its units work in its area, the job started first gets it, the others use their own production only.
    - Moving, attacking or starting another job cancels the job and its progress. A working unit has no movement left that turn and doesn't heal.
  - **Builder unit:** civilian (any attack destroys it), movement 2, sight 2, production cost 5, build production 2. Cities produce builders like any unit.
  - **Work area (stand-in for G7):** the tiles at distance 1 from a city (`cities.workRadius: 1`). The current spacing of 3 means two work areas never overlap; the validator keeps `minimumSpacing > 2 × workRadius`. Ownership is derived from the city, so a captured city's improvements change sides with it.
  - **Resources (stand-in for G5):** a ruleset list `resources` (food, wood, stone, gold, science, culture) with texts. Each player has a stockpile; at the end of the owner's turn every city adds its buildings' yields and the yields of the improvements in its work area. Nothing spends the stockpile yet.
  - **Tile improvements** (built by a builder on a work-area tile with the right terrain, at most one per tile). With a builder (2) and the city (1), a cost of 6 takes 2 turns:

    | Improvement | Terrain | Cost | Yield per turn |
    |---|---|---|---|
    | Farm | plains | 4 | 2 food |
    | Lumber camp | forest | 4 | 2 wood |
    | Mine | hills | 6 | 2 stone |

  - **City buildings** (produced by the city with production, at most one of each per city, kept on capture):

    | Building | Cost | Gives per turn | Later (not in this plan) |
    |---|---|---|---|
    | Army camp | 8 | +1 production to its city | faster army production |
    | Campus | 8 | 2 science | science tree (G11) |
    | Trade post | 8 | 2 gold | traders |
    | Culture centre | 8 | 2 culture | culture tree (G11), territory growth (G7) |
    | Harbour | 8 | 2 food; needs a lake next to the city | ships (G15) |

  - **Production of buildings:** a city's production is a unit **or** a building (`ProductionItem`). `SetProductionCommand` takes either; a building already in the city, or a harbour away from a lake, is refused. When a building completes, the city switches back to `cities.initialProduction`.
  - **Bridges:** a builder or a soldier builds a bridge on a river edge of its own tile (any river kind, anywhere on the map, outside enemy work areas), cost 6: 2 turns for a builder at home, 6 for a soldier far from home. Units of both players cross a bridged edge as if it were a stream: no extra movement, no river defence bonus. All crossing already goes through G6's one step check, so only that check and the combat modifier look at the bridge flag.
  - **Fog of war:** improvements and bridges show on **currently visible** tiles (a bridge when either side is visible); a city's buildings and production stay private to its owner; the stockpile is in the owner's view only.
  - **Ruleset v5:** `rulesets/default/v5.json` (v4 untouched) adds the builder, `resources`, `buildings` (kind `improvement` or `city`, cost, terrains, yields, `production`, `requiresAdjacentTerrain`), `cities.workRadius`, units' `buildProduction`, the `build_bridge` ability, the bridge cost (`bridges.cost`) and texts. The Api and client embed v5.
  - **Client** (only what these mechanics need): the unit panel lists the valid "Build farm / mine / …" actions for a selected builder with the turns each would take, and shows a working unit's job and progress with a cancel button; "Build bridge" (builders and soldiers) is a two-step target mode like attacking (tiles across a bridgeable edge light up); improvements and bridges get board sprites; the city panel's production picker lists buildings next to units and shows the city's buildings; the top bar shows the stockpile with the planned `ResourceBadge`; a selected city or builder outlines the city's work area; `/design` gallery entries.
- Out (deferred):
  - Other units building, and per-unit build production growing with experience or techs: later.
  - **G10b:** upgrades (farm → irrigated farm …, the roadmap's chains), building health, pillage and repair, removing or replacing an improvement, an enemy unit on a tile blocking its yield.
  - Spending resources, resource costs for units and buildings: G5, G8, G11.
  - Traders, faster army production, ships from harbours: later items (ships are G15).
  - Real territory and its growth: G7, which replaces the work-area stand-in. Unlocking buildings through science or culture nodes: G11.
  - A production queue: G8 (`QueueList` stays planned).
  - Housing from buildings: G8.

## Affected areas
- Ruleset: `rulesets/default/v5.json` (new; v1 to v4 untouched).
- Core:
  - `Buildings/BuildingType.cs` (id + JSON converter, like `UnitType`), `Buildings/BuildingDefinition.cs`, `Buildings/BuildingKind.cs`, `Resources/ResourceType.cs`, `Resources/ResourceDefinition.cs`.
  - `Units/UnitAbility.cs` (+ `Build`, `BuildBridge`), `Units/UnitDefinition.cs` (+ `BuildProduction`), `Units/Unit.cs` (+ `Job`, left out of JSON when idle), `Units/UnitJob.cs` (target improvement or bridge edge, progress, start order).
  - `Map/Tile.cs` (+ `Improvement`, + bridges on its three owned edges, both left out of JSON when empty), `Map/GameMap.cs` (`BridgeBetween`, step check and step cost treat a bridged edge as a stream).
  - `Cities/City.cs` (`Producing` becomes `ProductionItem`, + `Buildings`), `Cities/ProductionItem.cs`, `Cities/WorkArea.cs` (the tiles a city works).
  - `State/Player.cs` (+ `Resources`), `State/GameState.cs` (schema version bump).
  - `Actions/BuildImprovementAction.cs`, `Actions/BuildBridgeAction.cs`, `Actions/CancelJobAction.cs` (new), `Actions/MoveUnitAction.cs` and `Actions/AttackAction.cs` (cancel the job; capture keeps buildings), `Actions/SetProductionAction.cs`, `Actions/EndTurnAction.cs` (job progress with unit and city production, completion, yields, city buildings).
  - `Engine/CombatCalculator.cs` (no river modifier across a bridge), `Engine/RuleViolations.cs` (new codes).
  - `Events/ImprovementBuiltEvent.cs`, `Events/BridgeBuiltEvent.cs`, `Events/BuildingCompletedEvent.cs`.
  - `Rulesets/BuildingRules…`, `Rulesets/Ruleset.cs`, `Rulesets/RulesetDocument.cs`, `Rulesets/CityRules.cs`, `Rulesets/RulesetValidator.cs`.
  - `Visibility/TileView.cs`, `Visibility/CityView.cs`, `Visibility/PlayerView.cs`, `Visibility/UnitView.cs` (the job and its progress for own units only), `Visibility/VisibilityCalculator.cs`, `Visibility/ViewPreviewCalculator.cs` (build options, bridge targets, production options).
  - `Serialization/GameSerializer.cs`: read the previous schema, where `producing` is a bare unit id.
  - Reuse: `GameMap.RiverBetween` and `HexEdge`, `ReachabilityCalculator`'s step check, `GameStateBuilder` (+ `WithImprovement`, `WithBridge`, `WithBuilding`), `TestRulesets.With`.
- Application / client: embed v5 in `Govern.Application.csproj` + `Games/Services/DefaultRulesetService.cs`, `Govern.Client.csproj` + `Features/Games/Services/RulesetService.cs`. The Api's command endpoint already takes any `GameCommand`; the two new commands join its JSON polymorphism.
- Client:
  - `Features/Games/Components/Board/BoardImprovements.razor` (new layer), `Features/Games/Components/Board/BoardRivers.razor` (bridge plank), `GameSprite` icons for the three improvements, the builder and the five buildings.
  - `Features/Games/Components/UnitPanel.razor` (build actions with turns, job progress, cancel), `Features/Games/Components/CityPanel.razor` (picker with buildings, built list), the top bar (stockpile), `Components/Game/ResourceBadge.razor` (from the G1 catalog).
  - `Features/Games/Services/GameUiService.cs` (bridge target mode), `Features/Design/Pages/DesignPage.razor`.

## Steps
1. Core, ruleset data: `resources`, `buildings`, units' `buildProduction`, `bridges.cost`, `cities.workRadius` records; validator checks with JSON paths (unique ids, known terrains and resources, costs ≥ 1, yields ≥ 0, a unit with `build` or `build_bridge` has build production ≥ 1, `requiresAdjacentTerrain` known, spacing vs work radius, a text per resource and building).
2. Core, `rulesets/default/v5.json`: v4 plus the builder, the tables above and texts.
3. Core, state: `ProductionItem`, `City.Buildings`, `Tile.Improvement`, bridges, `Unit.Job`, `Player.Resources`; schema bump and reading the previous version.
4. Core, bridges: the step check and step cost accept a bridged edge; no river modifier across it. Lands first, on its own commit, since it touches movement.
5. Core, actions: build improvement, build bridge, production of buildings, end-turn yields and completion, capture keeps buildings.
6. Core, view: tiles, cities, stockpile, own units' jobs; previews for build options and bridge targets with the turns they take, and production options.
7. Application and client: embed v5; the Api accepts the new commands.
8. Client: board layer and sprites, unit and city panels, bridge target mode, stockpile in the top bar, work-area outline, gallery.

## Tests
- Unit (Core):
  - `RulesetTests`: the new sections parse; each validator check fails with its path.
  - `BuildImprovementActionTests`: starts a job on a matching work-area tile; refused off the work area, on the wrong terrain, on a tile that already has one, with no movement, by a soldier or settler, on an enemy city's tile.
  - `BuildBridgeActionTests`: a builder and a soldier start a bridge on a stream, river and deep river edge; refused with no river, an existing bridge, in an enemy work area, by a unit without the ability.
  - `EndTurnActionTests` (jobs): progress adds the unit's build production, plus the city's inside its work area while the city's own item gets nothing that turn; outside it the unit's alone; one city feeds only the oldest job; the improvement or bridge appears at the cost and the unit stays; moving, attacking or cancelling drops the job.
  - `MoveUnitActionTests` / `CombatCalculatorTests`: a bridged deep river is crossed at the tile's cost, from either side; melee across a bridge gets no river bonus.
  - `SetProductionActionTests`: a building can be chosen; refused when already built or a harbour has no lake.
  - `EndTurnActionTests`: improvements and buildings add their yields to the owner's stockpile; the army camp adds production; a finished building is added and production switches back; yields of a captured city go to the new owner.
  - `GameEnginePreviewTests`: parity of build options, bridge targets and production options with `Execute` in scenarios with each terrain, river kind and building.
  - `VisibilityCalculatorTests`: improvements and bridges only on visible tiles; enemy buildings, production and stockpile hidden.
  - `GameSerializerTests`: a v4-era snapshot reads with `producing` as a unit; new fields round-trip and are left out when empty.
- Integration: none new beyond the command round trip already covered; existing game tests run on v5, plus one Api test that a build command is accepted and changes the view.
- Client (bUnit): `UnitPanel` lists only valid build actions with their turns, and a working unit's progress and cancel; `CityPanel` lists buildings and hides built ones; `BoardImprovements` draws one sprite per visible improvement; bridge target mode highlights and sends the command; `ResourceBadge` and the top bar stockpile.
- Architecture: none new (Core folders `Buildings/` and `Resources/` added to the `FolderStructureTests` whitelist). `RulesReferenceTests` gains "Improvements" and "City buildings" tables checked against v5.

## Docs & rules
- `docs/game/rules.md`: building with builders and soldiers (own and city production), work area, improvements and city buildings tables, bridges, resources.
- `docs/game/game-data.md` (`resources`, `buildings`, `buildProduction`, `bridges.cost`, `workRadius`, v5; drop G10a from "Future sections"), `docs/game/game-logic.md` (yields at end of turn, bridges in the step check), `docs/features/game-core.md`, `docs/features/game-ui.md` (improvements layer, `ResourceBadge` built, bridge target mode), `docs/game/design-roadmap.md` (G10 split into G10a and G10b, the stand-ins G7 and G5 replace), `docs/README.md`.
- `.claude/rules/game-core.md`: production is a unit or a building; the step check is the only place a river or bridge is read.
- Backlog: G10 becomes G10a (this plan) and G10b; on approval G10a moves to Now (owner's call). Close-out (plan to `docs/plans/done/`, backlog "Recently done") goes in the feature PR.

## Verification
- `dotnet build Govern.slnx`, `dotnet format Govern.slnx --verify-no-changes`, `dotnet test Govern.slnx`: all green, summary shown.
- Run the Api and client against a Postgres container and play both seats: produce a builder, build a farm, mine and lumber camp and watch the city's production go to them, bridge a deep river with a soldier away from home and walk across, produce a campus and a harbour, watch the stockpile grow, capture a city and see its buildings change sides; light and dark, phone width. Screenshots in `/mnt/project-files/screenshots/g10-builders/`.

## Risks & open questions
- **City production paused while a unit works at home:** the city's own unit or building waits while a builder uses its production. That is the owner's model; the panel shows it so it isn't a surprise.
- **Soldiers as builders:** a soldier bridging has no movement and stays in place for several turns, which makes it an easy target. Intended as a trade-off.
- **Resources with nothing to spend on:** the stockpile only grows until G5, G8 or G11 gives it a use. The army camp's +1 production is the only yield that changes play today.
- **Work area of 1 tile:** gives each city at most 6 improvement spots. G7 replaces it with real territory; the radius is a ruleset number in the meantime.
- **Bridges anywhere, not only at home:** this lets a player bridge towards the enemy, which the enemy's units can use too. Enemy work areas are excluded so a player can't bridge into the other's cities.
- **Size:** about G9's size (new state on three records, two actions, previews, a board layer, two panels). The bridge step lands first and alone.

## Decisions made during implementation

- **Names and folders:** the abilities are `build_improvement` and `build_bridge`; the ruleset has separate `improvements` and `buildings` lists (no `kind` field); the new Core types live in one `Core/Economy` folder (ids, `ResourceAmount`, definitions), with `Cities/WorkArea` and `Units/UnitJob`.
- **No schema bump:** a city keeps `Producing` (its unit) and gains an optional `ProducingBuilding`, set by a new `ProduceBuildingCommand` (`produce_building`) instead of a `ProductionItem` in `SetProductionCommand`. Every new field is left out of the JSON when empty, so stored games read unchanged. A finished building sends the city back to its unit (not to `initialProduction`); `SetProductionCommand` clears a building in production; a capture clears it and keeps the buildings.
- **No cancel command:** moving, attacking or starting another job cancels a job, so `CancelJobAction` was dropped.
- **Yields timing:** jobs advance before yields are collected, so an improvement yields at the end of the turn it is finished; buildings complete after the yields, so a building yields from the next turn.
- **Bridges:** stored on the edge's owner tile like rivers (`TileBridges`) and read only through `CrossedRiverBetween` (step cost, combat river modifier, capture check). A bridge shows while the tile that stores the edge is visible. A bridge builder's sight must be at least `workRadius` + 1 (validator), so the "enemy work area" check is exact in the preview.
- **Client:** the action bar shows the improvements that fit the unit's terrain, with the rule's reason when disabled; the unit panel shows a job's progress and its turns at the unit's plus the covering city's production. The stockpile reuses `CostLabel` (no separate `ResourceBadge`). The work-area outline was left out (nothing needs it yet). Bridge mode is `BoardSelection.Bridge` with `BoardHighlights.Bridgeable`; the movement-cost lens leaves bridged edges unmarked.
- **Tests:** the build previews have their own parity tests (`Engine/BuildPreviewTests`); construction at the end of a turn is in `Engine/ConstructionRuleTests`. The Api test founds a city and sends `produce_building` (accepted, and an unknown building is a 409 with its code) in `SendGameCommandEndpointTests`.
- **Not done here:** the manual two-seat play-through with screenshots.
