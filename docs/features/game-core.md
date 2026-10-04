---
title: Game Core engine
tags: [game, core, engine, trading, trade-route, trader, market, deal, demand, upkeep, resource-cost, supply, building-health, upkeep-priority, hex, rivers, edges, bridges, builders, jobs, improvements, buildings, territory, tile-yields, city-stock, economy, rules, ruleset, abilities, fog-of-war, view-version, previews, ids, commands, events, game-log, events-for, serialization, schema-version, determinism]
code: [src/Govern.Core, tests/Govern.UnitTests/Core]
updated: 2026-10-04
---

# Game Core engine

`Govern.Core` is the rules engine of Govern's 2-player, turn-based hex strategy game, a much-simplified Civilization. It is a pure library: the base class library only, no I/O, no clock, no `System.Random` and no project or package references. The Api uses it to validate every move authoritatively, and the Blazor client uses the same code to preview moves from its `PlayerView` ([view previews](#view-previews-for-the-client)). Why it is built this way: [ADR 0004](../decisions/0004-game-engine.md). Delivery status: [game delivery plan](../plans/done/game-delivery.md), [ruleset A](../plans/done/ruleset-a-core.md) (done; [B](../plans/deferred/ruleset-b-store-and-pin.md) deferred).

**Every number, list and text comes from the game's ruleset** (`Govern.Core.Rulesets.Ruleset`, the JSON document in `rulesets/`): unit and terrain stats, city rules, map sizes, spawn zones, starting units and names. Core holds the mechanics only. The format, defaults and validation are in [game data](../game/game-data.md); every entry point below takes the ruleset as its first parameter.

## Engine entry points (GameEngine)

| Member | Purpose |
|---|---|
| `Ruleset.Parse(json)` / `ParseValid(json)` | Reads, validates and hashes a ruleset document ([game data](../game/game-data.md#reading-a-ruleset-in-code)) |
| `GameFactory.Create(ruleset, GameSetup)` | New game: the setup's predefined map (`GameSetup.Map`, a `PredefinedMap` from the pool, [maps](../game/maps.md)) with a spawn candidate per player, or a generated map of the ruleset's size (or the setup's size, within the ruleset's limits) whose two halves mirror each other ([map generation](#map-generation)); each player gets the ruleset's starting units (by default a settler plus an adjacent soldier) on the default terrain. Turn 1, first player, version 0 |
| `GameEngine.Execute(ruleset, state, actor, command)` | Validates and applies one `GameCommand`. Returns `CommandResult`: a `GameUpdate` (new `GameState` + ordered `GameEvent`s) or a `RuleViolation`. A rejected command never changes the state |
| `GameEngine.ReachableTiles(ruleset, state, viewer, unitId)` | Tiles one of the viewer's units can move to this turn, for UI highlights. Another player's unit always returns an empty list |
| `GameEngine.ReachableTiles(ruleset, view, unitId)`, `AttackTargets(ruleset, view, unitId)`, `CombatForecast(ruleset, view, unitId, target)`, `CheckFoundCity(ruleset, view, unitId, name?)`, `CheckBuildImprovement(ruleset, view, unitId, improvement)`, `BridgeTargets(ruleset, view, unitId)`, `CheckProduceBuilding(ruleset, view, cityId, building)`, `BuildingSites(ruleset, view, cityId, building)`, `CityIncome(ruleset, view, cityId)`, `CityForecast(ruleset, view, cityId)`, `UnitSupply(ruleset, view, unitId)`; `TradePreviewCalculator.CheckOpenTradeRoute`/`CheckProposeDeal`/`CheckAcceptDeal` | What `Execute` would decide for a move, an attack (and the damage range of a fight), founding a city, starting an improvement or a bridge, producing a building, or a city's income, upkeep and a unit's supply at the end of the turn, or a trade command, worked out from a `PlayerView` alone, for the client; nothing on the opponent's turn or in a finished game ([view previews](#view-previews-for-the-client)) |
| `GameEngine.ViewFor(ruleset, state, player)` | The fog-of-war `PlayerView`: the only shape a client may receive |
| `GameSerializer` | The one JSON format for storage (jsonb), the Api and the client |

After each accepted command, `Execute`:
1. runs the command's rule;
2. runs `VictoryRule` (eliminations, removal of departed players' units, win check);
3. updates every active player's explored tiles;
4. increments `GameState.Version` by one (an internal command count; clients never see it, see [View version](#view-version)).

## Hex map and coordinates (HexCoord)

- **Pointy-top hexes in axial coordinates:** `HexCoord(Q, R)`. `S = -Q - R` is derived.
- **Rectangular maps** use the **odd-r offset layout**: odd rows are shifted half a tile right. `HexCoord.FromOffset(column, row)` converts from offset coordinates, and `Column`/`Row` convert back.
- **Distance** is `(|ΔQ| + |ΔR| + |ΔS|) / 2`. `Neighbors()` always returns the six tiles in the same order (east, then counter-clockwise), which keeps pathfinding deterministic.
- **Map size:** `GameMap.Width × Height` (the ruleset's default, 20×14, within its limits, 8–40). Tiles are stored row by row. Terrain rules come from the ruleset (`Ruleset.Terrain`): movement cost, passable, sight bonus.
- **Mirror:** `GameMap.MirrorOf(coord)` is the tile opposite through the map's centre (column `W − 1 − c`, row `H − 1 − r`); on an even height it keeps neighbours neighbours.

## Terrain and map generation

| Terrain (default ruleset) | Movement cost | Passable | Sight bonus | Defence bonus |
|---|---|---|---|---|
| Plains (default terrain) | 1 | yes | 0 | 0 |
| Forest | 2 | yes | 0 | +3 |
| Hills | 2 | yes | +1 | +3 |
| Mountains | — | no | 0 | 0 |
| Lake | — | no | 0 | 0 |

- **Predefined maps** (`Map/PredefinedMap`, parsed by `PredefinedMapParser`, checked by `PredefinedMapValidator` and `PredefinedMapBalanceValidator`): a hand-made map from `maps/<key>.json` with its spawn candidates, validated against the ruleset (size, legend, mirror symmetry of tiles and rivers, connectivity, spawn rows). `GameFactory` plays it as it is and draws a candidate per player. Format and checks: [maps](../game/maps.md).
- **Map generation** (`Map/MapGenerator`, internal, called by `GameFactory` after the spawns): patches of each `map.generation.patches` terrain grow from random seeds until they cover their share; every tile is written with its mirror, so both halves are the same. Spawn tiles and their mirrors stay default terrain; an impassable tile is placed only while all passable tiles stay connected (flood fill). The spawns themselves are not mirrored, so the map narrows the opponent's start only slightly (their starting tiles are plains). Every draw goes through `DeterministicRandom.NextHashed` (SHA-256 of the raw draw), because the whole map reaches the clients and raw `Next` results would expose the generator's low bits. Algorithm: [game logic](../game/game-logic.md#map-generation).
- **Sight bonus:** `VisibilityCalculator.VisibleTiles` adds the `SightBonus` of the tile a unit or city stands on. It only adds sight, so the fog invariants the previews rely on still hold.
- **Defence bonus:** added to a defending unit's strength in `CombatCalculator` (G9).
- **Deferred:** forests blocking sight (needs line of sight and breaks the "sees every reachable tile" invariant), terrain layers such as forested hills (G5).

## Rivers (G6)

| River (default ruleset) | Crossing cost | Crossable | Defence bonus |
|---|---|---|---|
| Stream | 0 | yes | 0 |
| River | +1 | yes | +3 |
| Deep river | — | no | +5 |

- **State:** rivers run along tile edges. `HexEdge.Between(a, b)` names an edge by the tile that stores it and the `HexSide` (east, north-east, north-west), so every edge is stored once, in that tile's `Tile.Rivers` (`TileRivers`, one optional `RiverType` per side). `GameMap.RiverBetween(a, b)` and `PlayerView.RiverBetween(a, b)` look an edge up from either side. A tile without rivers leaves `rivers` out of the JSON, so games saved before G6 read unchanged with no schema bump.
- **Movement is per step:** `ReachabilityCalculator` prices each step between neighbours with `StepCost` (the entered tile's cost plus the river's `crossingCost`; null over an uncrossable river), shared by `MoveUnitRule` and the view preview.
- **Combat:** `AttackTarget.River` carries the river between attacker and target. `CombatCalculator` adds it as a `River` modifier for the defender of a melee attack; `AttackRule.Check` refuses capturing an undefended city across an uncrossable river (`attack.river_in_the_way`).
- **Generation** (`Map/RiverGenerator`, internal, called by `MapGenerator` after the terrain): walks along corners from a source terrain to a mouth terrain or the border, edge by edge with the mirror, deepening by `map.generation.rivers.stages`; an uncrossable edge is laid only while the passable tiles stay connected. Draws use `NextHashed`. Algorithm: [game logic](../game/game-logic.md#rivers).
- **View:** `TileView.Rivers` for explored and visible tiles (rivers never change).
- **Bridges** (G10a) sit on edges like rivers: see [Builders, improvements, buildings and bridges](#builders-improvements-buildings-and-bridges-g10a).
- **Deferred:** ships on rivers (G15), seasonal river states (G14).
- Reference: Red Blob Games, "Hexagonal Grids".

## Units and cities (the default ruleset)

| Unit | Class | Strength | Movement | Sight | Cost | Abilities |
|---|---|---|---|---|---|---|
| Settler | civilian | — | 2 | 2 | 6 | `found_city`: founds a city (consumed) |
| Soldier | melee | 20 | 2 | 2 | 4 | `attack`: fights adjacent enemy units; captures undefended enemy cities |
| Spearman | anti_cavalry | 18 | 2 | 2 | 4 | `attack` |
| Horseman | cavalry | 20 | 3 | 3 | 6 | `attack` |
| Archer | ranged | 10 (shoots at 22, range 2) | 2 | 2 | 5 | `ranged_attack`: shoots enemy units in range, no damage back |
| Builder | civilian | — | 2 | 2 | 5 | `build_improvement`, `build_bridge` (build production 2) |

The soldier also has `build_bridge` with build production 1.

- `UnitType` and `TerrainType` are ids from the ruleset (`readonly record struct`s written to JSON as the plain id). Stats come from `ruleset.Unit(type)`; rules check abilities (`UnitDefinition.CanFoundCity`/`CanAttack`/`CanRangedAttack`, `AttackRange`), never a unit id. A new unit is a ruleset entry only; a new ability also needs a `UnitAbility` value, a command and a rule.
- **Cities** (the ruleset's `cities` section; defaults in brackets):
  - sight (2);
  - production per turn at the end of the owner's turn (1);
  - minimum spacing from any other city (3);
  - initial production (Soldier);
  - named by the player when founded (the ruleset's `suggestedNames` are only offered by the client);
  - a captured city's progress resets (yes).
- **Stacking:** at most one unit per tile. A unit may stand in its own city.

## Builders, improvements, buildings and bridges (G10a)

Andrii's model, not a copy of another game: units have no build charges; a building unit has its own **build production** and, in its own city's territory, the city's production is added.
- **Folder:** `Core/Economy` holds the ids (`ResourceType`, `ImprovementType`, `BuildingType`, plain ids in JSON like `UnitType`), `ResourceAmount` and the definitions (`ResourceDefinition`, `ImprovementDefinition`, `BuildingDefinition`). Ruleset sections: [game data: building sections](../game/game-data-economy.md).
- **State, all optional in the JSON (no schema bump):** `Unit.Job` (`UnitJob`: progress, the round it started, and an `Improvement` or a `BridgeTo` target), `Tile.Improvement`, `Tile.Bridges` (`TileBridges`, one flag per stored side, like `TileRivers`), `City.ProducingBuilding` and `City.Buildings`; and since G10c `Tile.Building` and `City.ProducingBuildingSite`.
- **City production:** `Cities/CityProduction.PerTurn` (`productionPerTurn` plus the buildings' `production`).
- **Building sites (G10c):** every building stands on its own tile of its city's territory. `ProduceBuildingCommand` carries the `Site`; `ProduceBuildingRule.Check` (city: unknown, already built) and `CheckSite` over an `Engine/BuildingSite` (in this city's territory and off the city tile, passable, no improvement or building, no own improvement job there, the `requiresAdjacentTerrain` next to the site) decide. The city reserves the site (`City.ProducingBuildingSite`, set exactly with `ProducingBuilding`; cleared by `SetProductionCommand`, a capture or completion); a reserved or built tile refuses improvements (`ImprovementSite.HasBuilding` → `build.tile_has_building`). On completion the building joins `City.Buildings` (still what upkeep, damage, yields and production read) and is written to `Tile.Building`; `BuildingCompletedEvent.Position` is the site. A building in production with no site (a game saved before G10c) finishes off the map.
- **Commands:** `BuildImprovementCommand` (`build_improvement`), `BuildBridgeCommand` (`build_bridge`), `ProduceBuildingCommand` (`produce_building`). Starting a job sets movement to 0 (`BuildStartedEvent`); moving or attacking drops the job; `SetProductionCommand` clears a building in production.
- **End turn** (`EndTurnRule`): note the resting units, then `Engine/ConstructionRule.AdvanceJobs` (oldest job first; each city feeds one job and then skips its own production; a job whose placement check fails is dropped; `ImprovementBuiltEvent`, `BridgeBuiltEvent`), then `ConstructionRule.CollectYields` (each city's income into its stock, see below), then `UpkeepRule.Apply` ([G8a](#costs-and-upkeep-g8a)), then `TerritoryRule.Grow`, then production (`BuildingCompletedEvent` for a building).
- **Bridges** are only seen through `GameMap.CrossedRiverBetween` / `PlayerView.CrossedRiverBetween` (null when the edge is bridged), used by the step cost, the combat river modifier and the capture check, so a bridged edge is like no river.
- **View:** `TileView.Bridges` and `Improvement` for visible tiles only (they change); `UnitView.Job` for own units only; `CityView.ProducingBuilding`, `ProducingBuildingSite` and `Buildings` for own cities only; `TileView.Building` for visible tiles only, like improvements.
- **Previews:** `BuildImprovementRule.Check`, `BuildBridgeRule.Check` and `ProduceBuildingRule.Check`/`CheckSite` are shared by the rules and `ViewPreviewCalculator`; `GameEngine.BuildingSites` lists the tiles a building can take and `CheckProduceBuilding` gives the reason of the tile that came closest when none can; `Engine/BuildPreviewTests` checks parity with `Execute` (every site within 2 of the city).
- Algorithms: [game logic](../game/game-logic.md#building-with-units-jobs). Later (G10b): upgrades, pillage and repair, and the buildings' own mechanics.

## Tile yields and territory (G5, G7)

Andrii's model: every tile gives resources, a city owns the tiles of its territory, and each city keeps its own stock (ruleset v6, [game data](../game/game-data-economy.md#tile-yields-terrainsyields-riversyields)).
- **State:** `Tile.Territory` (the owning `CityId`, left out of the JSON when null), `City.Stock` (`StockList`, in ruleset order) and `City.TilesGained`. `Player.Resources` is gone (schema 3).
- **Yields:** `Economy/YieldCalculator` (public, used by the client): `TileYields` = terrain yields + per resource the largest yield of the rivers on the tile's edges (`GameMap`/`PlayerView.RiversAround`) + the improvement's; `CityIncome` = the territory's tile yields + `cities.yields` + the buildings' yields; `Sum` and `AmountOf`.
- **Territory:** `Engine/TerritoryRule` (internal). `ClaimAround` runs when a city is founded (`initialRadius`, unowned tiles only); `Grow` runs at the end of the owner's turn: each city, by id, that holds `TerritoryRules.NextTileCost(TilesGained)` of the growth resource pays it and claims `NextTile` (unowned, touching the territory, within `maxRadius`; highest total yield, then nearest, then row, then column), emitting `TerritoryGrewEvent` (`territory_grew`). No randomness, so no draw changes.
- **Rules that read territory:** founding on any territory tile is `city.in_territory`; an improvement needs a tile in an own city's territory that is not a city tile (`build.not_in_territory`); a bridge end in enemy territory is `bridge.enemy_territory`; a job is fed by the own city whose territory holds the unit's tile. A captured city keeps its territory and stock.
- **View:** own territory is always visible; `TileView.TerritoryOwner` (visible tiles only) and `TerritoryCity` (the viewer's own territory only); `CityView.Stock` and `TilesGained` for own cities only. `GameEngine.CityIncome(ruleset, view, cityId)` previews a city's income and equals what the end of turn adds (`TerritoryRuleTests`).
- Algorithms: [game logic](../game/game-logic.md#territory).

## Costs and upkeep (G8a)

Andrii's model: everything a city makes costs resources once and upkeep every turn, a city keeps up what stands on its territory, and a shortage hurts instead of killing (ruleset v7, [game data](../game/game-data-economy.md#resource-costs-and-upkeep-resourcecost-upkeep-upkeep-section)).
- **Ruleset:** `UnitDefinition.ResourceCost`/`Upkeep`, `BuildingDefinition.ResourceCost`/`Upkeep`, and `Rulesets/UpkeepRules` (`supplyRadius`, `shortageDamage`, `buildingHealth`, `buildingRecovery`), checked by `Rulesets/UpkeepRulesValidator`.
- **State, all optional in the JSON (no schema bump):** `City.ProductionPaid` (the item's resource cost is paid), `City.UpkeepPriority` (`Cities/UpkeepPriority`: `buildingsFirst`, the default, or `unitsFirst`) and `City.BuildingDamage` (`Cities/BuildingDamage` per hurt building; `DamageOf`, `WorkingBuildings`). A unit's shortage is ordinary `Unit.Damage`.
- **Paying:** `Economy/UpkeepCalculator` (public, used by the client): `Pay` (buildings in ruleset order and units most hurt first, in the city's priority, each all or nothing), `Missing`, `Subtract` and `DamageAfter` (each building's damage after the upkeep, shared by the rule and the forecast). `Cities/CityProduction.ResourceCost` and `Refund`.
- **End turn:** `Engine/UpkeepRule.Apply` (internal) runs after the income: each city pays from its stock; an unpaid building takes `shortageDamage` (`BuildingDamagedEvent`, `building_damaged`) and stops yielding and producing at full damage; a paid one recovers `buildingRecovery`; a unit off its owner's territory is supplied by `LandYields` within `supplyRadius` (nothing taken, tiles not split); an unpaid or unsupplied unit takes `shortageDamage` down to 1 health (`UnitStarvedEvent`, `unit_starved`); a supplied unit that rested heals. Production then pays the item's cost first and waits while the stock can't cover it.
- **Changing production:** `SetProductionCommand` and `ProduceBuildingCommand` refund a paid cost when the item really changes (the same item is a no-op); a captured city's item is unpaid again.
- **Command:** `SetUpkeepPriorityCommand` (`set_upkeep_priority`): own city, a known priority (`UpkeepPrioritySetEvent`, `upkeep_priority_set`).
- **View:** `CityView.ProductionPaid`, `UpkeepPriority` and `BuildingDamage` for own cities only. Previews (`Visibility/UpkeepPreviewCalculator`): `GameEngine.CityForecast` (`CityForecast`: income, the `UpkeepResult`, what is missing to begin and the production per turn after upkeep, both assuming the city doesn't feed a job) and `GameEngine.UnitSupply` (`UnitSupply`: the paying city or the land's shortfall). `Engine/UpkeepPreviewTests` checks both against `Execute`.
- Algorithms: [game logic](../game/game-logic.md#costs-and-upkeep).

## Trading (G17)

Andrii's model: no money (gold is a resource), cities with a trade post swap resources through traders that keep a deal alive by walking between the two posts (ruleset v10, [game data](../game/game-data-economy.md#trade-trade), algorithms in [game logic: trading](../game/game-logic-trade.md)).
- **Ruleset:** `Rulesets/TradeRules` (`building`, `routesPerCity`, `maxCap`, `demandStepPercent`, `demandSmoothing`, `minDemandPercent`, `maxDemandPercent`), `ResourceDefinition.TradeValue` (tradable when ≥ 1, never the growth resource), the `trade` unit ability (`UnitDefinition.CanTrade`); checked by `TradeRulesValidator`.
- **State, all optional in the JSON (no schema bump):** `City.Market` (`Economy/MarketOrder`: resource, `MarketSide`, cap) and `City.Demand` (`ResourceDemand`, levels other than 100 %), `GameState.TradeRoutes` (`Economy/TradeRoute`: owner, home, host, trader, `TradeDeal`, opened, paused; `TradeRouteId` from the hashed generator) and `Unit.RouteId`.
- **Commands:** `SetMarketOrderCommand` (`set_market_order`), `OpenTradeRouteCommand` (`open_trade_route`), `ProposeDealCommand` (`propose_deal`), `AnswerDealCommand` (`answer_deal`, accept with routes to cancel, or decline), `CancelDealCommand` (`cancel_deal`), `CloseTradeRouteCommand` (`close_trade_route`); dispatched by `Engine/TradeRule.Dispatch`. Events: `TradeRouteChangedEvent` (`trade_route_changed`, with a `TradeChange`) and `TradeExchangedEvent` (`trade_exchanged`).
- **Engine:** `Engine/TradeRule` walks traders (through `AutoMoveRule.TakeSteps`, stopping next to the post), swaps goods after upkeep at the end of the host owner's turn and moves demand (`Economy/TradeCalculator`: free market, deal check, demand step), and after every command closes routes whose cities changed hands and breaks deals whose trader is gone. A trader on a route refuses move and set destination (`trade.on_route`); a trader is produced only next to a trade post.
- **View:** own cities' `CityView.Market`/`Demand`, `CityView.TradePost` for cities the viewer may trade with, `UnitView.RouteId` (owner only) and `PlayerView.TradeRoutes` (`TradeRouteView`: the owner's routes with the host's free market while open with the trader at the post; the routes at the viewer's cities without the trader or the home market). `CityForecast` ignores trade (the other side's stock is hidden).

## Science (G11a)

Andrii's model: science is a budget per turn, never stored; owned items take their points every turn and the newest switch off when the cities make too little (ruleset v11, [game data](../game/game-data-science.md), algorithms in [game logic: science](../game/game-logic-science.md)).
- **Ruleset:** `Rulesets/ScienceRules` (`resource`, `nodes`: `Science/ScienceNode` with points, requires, units, buildings), `UnitDefinition.Replaces`/`UpgradeCost`, `BuildingDefinition.Upgrades`; lookups `Ruleset.FindNode`, `NodeUnlocking`, `ReplacementOf`, `UpgradeOf`, `IsKindOf`, `NodeName`; checked by `ScienceRulesValidator`.
- **State, optional in the JSON (no schema bump):** `Player.Nodes` (`Science/OwnedNode`: node id and `Disabled`, in unlock order) and `UnitJob.UpgradeTo`.
- **Commands:** `UnlockNodeCommand` (`unlock_node`), `UpgradeUnitCommand` (`upgrade_unit`), `UpgradeBuildingCommand` (`upgrade_building`, a builder's job); dispatched by `Engine/ScienceRule.Dispatch`. Events: `ScienceNodeChangedEvent` (`science_node_changed`, with a `ScienceNodeChange`; its player only), `UnitUpgradedEvent` (`unit_upgraded`) and `BuildingUpgradedEvent` (`building_upgraded`; the city id only for its owner).
- **Engine:** `ScienceRule.Income` (the science resource in the player's cities' income), `ScienceRule.Balance` after every command; `Science/ScienceCalculator` (locks, newest of a line, the next production, upgrades, `CheckUnlock`) is shared by the rules and the client. `ConstructionRule` finishes upgrade jobs (the city pays when done; a finished job waits) and `CollectYields` skips science.
- **View:** `PlayerView.Science` (`ScienceView`: income and nodes), the viewer's own only; previews in `Visibility/SciencePreviewCalculator` (`CheckUnlockNode`, `CheckUpgradeUnit`, `CheckUpgradeBuilding`).

## Combat (G9)

- **State:** `Unit.Damage` (0 = unhurt; health = `combat.maxHealth` − damage). Stored as damage so games saved before G9 read as unhurt with no schema bump; the JSON leaves it out while it is 0. The view carries `UnitView.Health` for every unit the viewer sees.
- **`Engine/CombatCalculator`** (internal) works out a fight from two `Combatant`s (type, health, terrain) and the river between them: strengths with their `CombatModifier`s (health, counter, terrain, river), and each side's damage range. `AttackRule` rolls within those ranges; `GameEngine.CombatForecast` returns them to the client. Integer maths only.
- **Rolls** use `DeterministicRandom.NextHashed`, defender first, and only for a side whose range isn't a single value (a ranged attacker and a civilian take fixed damage).
- **Events:** `UnitAttackedEvent` (`unit_attacked`: both units, both positions, damage dealt and taken), then a `UnitDestroyedEvent` for each unit brought to 0.
- **Healing** is part of `UpkeepRule` at the end of the turn: a hurt unit with full movement left and no job rested, and heals when it is supplied.
- Algorithm and numbers: [game logic](../game/game-logic.md#combat), [game data](../game/game-data.md#combat-combat).

## Game rules v1 (commands)

| Command (`type` in JSON) | Rule |
|---|---|
| `MoveUnitCommand` (`move_unit`) | Cheapest path (Dijkstra) to an empty tile within `MovementLeft`. Never through units or enemy cities. Emits `UnitMovedEvent` with the path |
| `SetDestinationCommand` (`set_destination`) | Own unit; `target` an explored tile with a path over the actor's knowledge (no visible unit on it), or null to cancel. Sets `Unit.Destination`, drops the job and walks at once (`UnitMovedEvent`); the unit walks on at the start of each of its owner's turns until it arrives, has no path, or sights an enemy (`UnitOrderEndedEvent`) (F19, [game logic](../game/game-logic.md#set-a-destination-auto-moves)) |
| `FoundCityCommand` (`found_city`) | Settler with movement left, not on a city, spacing respected, and a `name`: trimmed with inner spaces collapsed, 1 to `FoundCityCommand.MaxNameLength` (24) letters, digits, spaces, `'`, `-` or `.`, not one of the actor's own city names (ignoring case). The settler is removed and a city created with that name (`CityFoundedEvent`) |
| `SetProductionCommand` (`set_production`) | Own city; changes what it builds and keeps its progress, refunding a paid resource cost (`ProductionChangedEvent`) |
| `AttackCommand` (`attack`) | A unit with `attack` or `ranged_attack` and movement left; a target in range (adjacent for melee). Against a unit, both fight ([combat](#combat-g9)): `UnitAttackedEvent`, then `UnitDestroyedEvent` for each unit at 0 health; an undefended enemy city is captured by a melee attacker, which moves in (`CityCapturedEvent`). Movement drops to 0 |
| `BuildImprovementCommand` (`build_improvement`) | A builder with movement left in its cities' territory starts an improvement job ([G10a](#builders-improvements-buildings-and-bridges-g10a)) |
| `BuildBridgeCommand` (`build_bridge`) | A unit with `build_bridge` and movement left starts a bridge job over the river towards a neighbour |
| `ProduceBuildingCommand` (`produce_building`) | Own city; produces a building it doesn't have yet on a free `site` of its territory (needs the adjacent terrain next to the site, if any), keeps its progress (`ProductionChangedEvent` with `Building`); the same building on another site moves the reservation ([G10c](#builders-improvements-buildings-and-bridges-g10a)) |
| `SetUpkeepPriorityCommand` (`set_upkeep_priority`) | Own city; pays buildings first or units first from the next end of turn (`UpkeepPrioritySetEvent`) |
| `EndTurnCommand` (`end_turn`) | Jobs advance, yields are collected, upkeep is paid (units heal or starve), territory grows, then the actor's cities pay resource costs and produce. A finished unit spawns on the city or its first free neighbour with 0 movement; if the city is surrounded, it waits. The next active player gets full movement; the round (`Turn`) increases on wrap (`TurnEndedEvent`); then their units with a destination walk on |
| `ResignCommand` (`resign`) | Any active player, any time (`PlayerResignedEvent`) |

**Turn guard:** every command except Resign requires a running game and `actor == CurrentPlayer`. The unit or city must exist and belong to the actor.

**Elimination and victory:**
- A player with no cities and no settlers is eliminated (`PlayerEliminatedEvent`).
- Units of eliminated or resigned players are removed.
- When one active player is left, the game is `Finished`, `Winner` is set, and `GameWonEvent` is emitted.

## Rule violation error codes (RuleViolations)

Rule violations are returned as `RuleViolation(Code, Message)` (listed in `RuleViolations`), never thrown. The Api returns them as 409 with the same code and message ([games](games.md#sending-commands)). Codes are stable:

| Code | When |
|---|---|
| `game.finished` | The game is over |
| `game.not_a_player` | The actor is not an active participant |
| `game.not_your_turn` | Another player's turn |
| `unit.not_found` | The actor has no such unit (missing and "someone else's" are deliberately the same) |
| `unit.no_movement` | No movement left this turn |
| `unit.cannot_found_city` / `unit.cannot_attack` | Wrong unit type for the command |
| `move.occupied` / `move.unreachable` | A **visible** unit stands on the target / off map, own tile, blocked, too far, or a hidden unit is there |
| `move.no_path` | A destination is off the map, the unit's own tile, unexplored, impassable, or has no path over the tiles the actor knows |
| `city.tile_taken` / `city.too_close` / `city.in_territory` | Founding on a city / closer than the minimum spacing / on a tile in any city's territory |
| `city.name_invalid` / `city.name_taken` | A new city's name is empty, too long or uses another character / one of the actor's own cities has it (enemy cities never count) |
| `city.not_found` / `city.unknown_unit_type` | The actor has no such city / a unit type the game's ruleset doesn't have |
| `attack.not_adjacent` / `attack.out_of_range` / `attack.no_target` / `attack.own_target` | Attack target problems (`out_of_range` for ranged units; `no_target` also for a ranged unit aimed at an empty city) |
| `attack.river_in_the_way` | A melee unit would capture an undefended city across a river it can't cross |
| `unit.cannot_build_improvements` / `unit.cannot_build_bridges` | The unit type lacks the build ability |
| `build.already_building` / `build.unknown_improvement` / `build.not_in_territory` / `build.wrong_terrain` / `build.tile_improved` / `build.tile_has_building` | Improvement job problems |
| `bridge.not_adjacent` / `bridge.no_river` / `bridge.exists` / `bridge.impassable` / `bridge.enemy_territory` | Bridge job problems |
| `city.unknown_building` / `city.building_exists` / `city.building_needs_terrain` / `city.building_not_in_territory` / `city.building_wrong_terrain` / `city.building_site_taken` / `city.building_site_being_improved` | Building production problems |
| `city.unknown_upkeep_priority` | An upkeep priority Core doesn't know |
| `science.unknown_node` / `science.node_owned` / `science.needs_prerequisite` / `science.not_enough` / `science.locked` / `science.replaced`, `city.building_is_upgrade`, `upgrade.none` / `upgrade.not_in_territory` / `upgrade.missing_resources` | Science, lock and upgrade problems, in check order in [game logic: science](../game/game-logic-science.md) |
| `trade.*`, `market.cap_out_of_range` / `market.below_deals` | Trade command problems, in check order in [game logic: trading](../game/game-logic-trade.md#the-trade-commands) |

Exceptions are reserved for programming errors: an invalid `GameSetup`, an unknown command type, or `ViewFor` for a non-participant.

## Fog of war (PlayerView)

- A player sees every tile within the sight radius of their units and cities, and every tile of their cities' territory.
- Everything a player has ever seen accumulates in `Player.Explored`, plus the tiles next to their cities' territory (`VisibilityCalculator.UpdateExplored`), so the rivers on the territory's outer edges are known and `CityIncome` matches the end of turn.
- `PlayerView.Tiles` marks each tile as `Unexplored` (terrain and rivers hidden), `Explored` (terrain and rivers known) or `Visible`. A `TileView` carries no coordinates: tiles are row-major (index `row * Width + column`), so `PlayerView.CoordAt(index)` gives a tile's position and `TileAt(coord)` the tile at a position. That keeps every snapshot about a third smaller.
- `PlayerView.Units` and `PlayerView.Cities` contain the viewer's own objects and enemy objects **on visible tiles only**.
- **Enemy production is hidden:** cities are `CityView`s, and an enemy city's `Producing` and `ProductionProgress` are null. Its progress would tell the viewer when the next enemy unit appears. A change in enemy production therefore never changes the viewer's `Version`.
- **Enemy movement is hidden:** units are `UnitView`s (`UnitView.From`), and a visible enemy unit's `MovementLeft` is null. It would show how far the unit moved through fog this turn, or that it attacked. A change in enemy movement alone therefore never changes the viewer's `Version`. `PlayerView.UnitAt` and `FindOwnUnit` return `UnitView`s; the view previews rebuild the viewer's own `Unit` (whose movement is always present) for the rules' shared checks.
- `PlayerView.Players` lists ids and statuses without the opponents' exploration.
- **Rejections leak nothing:** a hidden unit on a move target gives `move.unreachable`, not `move.occupied`. Another player's unit or city id gives `*.not_found`.
- **Invariants that keep rejections leak-free:** unit sight ≥ movement / cheapest step, and the sight of every unit with `found_city` ≥ city spacing − 1. `RulesetValidator` enforces both, so a ruleset that breaks them is rejected at load time until the rules filter by visibility.
- **City names leak nothing:** the player names each city (F12); `city.name_taken` compares only with the actor's own cities, and the client's suggestion is a random unused one, so no name counts the founder's cities.
- **Known residual leak** (accepted): a unit that loses health without a fight, or doesn't heal while resting, shows its owner's land or city is short of its upkeep (accepted, [game logic](../game/game-logic.md#fog-of-war)).
- `PlayerId` values are chosen by the host. Govern uses the per-game seat id (`GamePlayer.Id`), never a user id, so the view reveals no account ids ([games](games.md)).
- Hosts must send clients `PlayerView`, never `GameState`.
- Nothing in a view counts the opponent's activity: `PlayerView.Version` is a fingerprint of the view itself, unit and city ids are hashed random draws (see below), and `Units`/`Cities` are ordered by id rather than creation. Raw `GameEvent`s may only reach a player through `VisibilityCalculator.EventsFor` ([below](#events-a-player-may-see-eventsfor)).

## Events a player may see (EventsFor)

`VisibilityCalculator.EventsFor(ruleset, before, update, viewer)` returns, in order, the events of one accepted command that a player may see; the server stores them as that player's [game log](games.md#game-log) (F11a). Events carry public fields only (`CityFoundedEvent` and `UnitCreatedEvent` name the owner, type and tile, never a whole city or unit). The **seen tiles** are the viewer's visible tiles before the command plus those after it. The owner of a thing always sees its events.

| Event | The other player sees it |
|---|---|
| `turn_ended`, `player_resigned`, `player_eliminated`, `game_won` | always |
| `unit_moved` | the path cut to the seen tiles; dropped when none is seen |
| `unit_attacked` | when either tile is seen or the defender is theirs; an attacker on an unseen tile has a null `attackerId` and `from` (written as null, so stored entries read back) |
| `city_captured` | when the city's tile is seen or the city was theirs |
| `unit_destroyed`, `city_founded`, `unit_created`, `improvement_built`, `territory_grew` | when the tile is seen; `unit_created` and `territory_grew` without their `cityId` (the view never says which enemy city a tile belongs to) |
| `bridge_built` | when the builder's tile (`from`) is seen, since the builder stays there |
| `trade_route_changed`, `trade_exchanged` | only by the two sides of the route |
| `production_changed`, `build_started`, `building_completed`, `building_damaged`, `upkeep_priority_set`, `unit_starved`, `unit_order_ended` | never: the view already shows what the opponent may know (an enemy unit's `Destination` is hidden in the view too) |

Hidden events are dropped, never counted, so the log adds nothing the two views don't already give (`EventVisibilityTests` checks every event type for both players).

## View previews for the client

The client only holds a `PlayerView`, never the `GameState`, so the engine answers "what may I do now?" from a view:

| Member | Answer | Equal to |
|---|---|---|
| `GameEngine.ReachableTiles(view, unitId)` | Tiles the unit can move to, row then column | the tiles a `MoveUnitCommand` would be accepted for |
| `GameEngine.AttackTargets(view, unitId)` | Tiles in range with an enemy unit (or, for melee, an enemy city) | the tiles an `AttackCommand` would be accepted for |
| `GameEngine.CombatForecast(view, unitId, target)` | Both strengths, their modifiers and each side's damage range; `null` for a capture or a rejected attack | the range `Execute` rolls in |
| `GameEngine.CheckFoundCity(view, unitId)` | `null`, or the `RuleViolation` | what a `FoundCityCommand` would return |
| `GameEngine.CheckBuildImprovement(view, unitId, improvement)` / `CheckProduceBuilding(view, cityId, building)` | `null`, or the `RuleViolation` (for a building: of the site that came closest, when no site is accepted) | what a `BuildImprovementCommand` / `ProduceBuildingCommand` would return |
| `GameEngine.BuildingSites(view, cityId, building)` | The tiles of the city's territory, row then column | each tile a `ProduceBuildingCommand` would accept as its site |
| `GameEngine.DestinationPath(view, unitId, target)` | The path (`DestinationPreview.Path`) and the coming turns it still needs (`Turns`, 0 when the movement left gets there), or `null` | whether a `SetDestinationCommand` would be accepted, and the steps its walk takes. Planned over the view's knowledge, which is exactly what the rule plans over (explored terrain and rivers, bridges, units and cities on visible tiles); `GameEnginePreviewTests` compares it with `Execute` for every tile of the map in every scenario |
| `GameEngine.BridgeTargets(view, unitId)` | The neighbours the unit can bridge to, row then column | the targets a `BuildBridgeCommand` would be accepted for |
| `GameEngine.CityIncome(ruleset, view, cityId)` | What the viewer's own city adds to its stock at the end of the turn (empty for another player's city) | what `EndTurnCommand` adds |
| `GameEngine.CityForecast(ruleset, view, cityId)` | The income, the upkeep paid, the unpaid buildings and units, and what the item still needs to begin; `null` for another player's city | what `EndTurnCommand` pays and charges |
| `GameEngine.UnitSupply(ruleset, view, unitId)` | The own city that keeps the unit up, or, outside territory, what the land around it lacks | whether `EndTurnCommand` starves the unit |

- All three give nothing (or the violation) when the game is over, it isn't the viewer's turn, or the unit isn't the viewer's, the same as the command guard.
- **Why a view is enough:** unit sight ≥ movement, founder sight ≥ city spacing − 1, ranged range ≤ sight, and the sight of a unit with upkeep ≥ `upkeep.supplyRadius` + 1 (enforced by `RulesetValidator`). Every tile a unit could reach, attack or be blocked by is therefore visible, and the view shows every unit and city on it. Reachability over a view enters only visible tiles.
- **They can't drift from the rules:** `AttackRule.Check` and `FoundCityRule.Check` are shared by the rule and the preview, as are the turn check (`CommandGuardRule.CheckTurn`) and the Dijkstra in `ReachabilityCalculator`. `GameEnginePreviewTests` compares each preview with `Execute` for every tile within 3 of the unit, over scenarios covering own and enemy blockers, an own city in reach, enemies just outside sight (where the view and the state differ), the map edge, no movement, the opponent's turn, an enemy unit id, a finished game, terrain: forest around the unit (with full and with 1 movement left), mountains and lake around it, a path around a lake, a unit on hills that sees further, an archer with targets in and out of range, and a horseman, and rivers: a stream, a river and a deep river around the unit (with full and with 1 movement left), a deep river wall, enemies across rivers, and a horseman along a river. `CombatForecastTests` checks over 1000 seeds that `Execute` rolls exactly the forecast's lowest and highest damage for each kind of fight. A new terrain type with a new cost or blocker adds its own parity scenarios.

## View version

`PlayerView.Version` is an opaque fingerprint of one player's view: the first 8 bytes of the SHA-256 of the view's `GameSerializer` JSON (with `version` 0), masked to a non-negative `long`.

- **Equal views have equal versions**, and anything hidden from the viewer can't change the value. A command count would tell the opponent how often the other player acted; the fingerprint doesn't.
- **Compare it for equality only.** It is neither ordered nor sequential. Clients echo it as `expectedVersion`, and the host rejects a command when the caller's current view has a different version ([games](games.md#sending-commands)).
- Each player has their own version for the same state, since their views differ.
- `GameState.Version` still counts accepted commands; it stays inside the state.
- The fingerprint depends on the exact `GameSerializer` bytes, so a serializer change (e.g. a .NET upgrade that escapes differently) changes every version once: each open client gets one `game.stale_version` and reloads.

## Determinism and randomness

- The same state, actor and command always produce the same result.
- Randomness (spawn rows, map generation, combat damage) comes from `DeterministicRandom` (SplitMix64). Its state is stored in `GameState.RngState`, so replaying a command log rebuilds the exact game on any .NET version.
- Pathfinding breaks cost ties by row, then column, so event paths never depend on the `PriorityQueue` implementation.
- The host must generate `GameSetup.Seed` with a cryptographically secure RNG and never send it to clients, because spawn positions follow from it.
- **Ids:** unit and city ids come from the same generator (`GameState.AllocateId`), including the four starting units. Each id is 31 bits of the **SHA-256 of a 64-bit draw**, redrawn when 0 or already used by a unit or city. Sequential ids would reveal how many units and cities the opponent created, and raw draws would be worse: SplitMix64 is invertible, so two raw ids let a player rebuild `RngState` in seconds and with it the opponent's spawn and every future id. Hashed ids reveal nothing and still replay identically.
- **Never send raw `DeterministicRandom` output to a client.** Hash anything a player can see (ids), or keep it to a few bits of a small range (spawn tiles). Guessing the state from hashed ids takes about 2⁶⁴ work, the same as guessing the 64-bit seed.

## JSON format and schema version (GameSerializer)

- `GameSerializer.Options` uses web defaults (camelCase), with enums as camelCase strings (numbers are rejected). `UnitType`, `TerrainType`, `RiverType`, `ResourceType`, `ImprovementType` and `BuildingType` are written as their plain ruleset ids (`"settler"`), the same JSON the former enums gave, so stored games read unchanged.
- Commands and events are polymorphic, with a `type` discriminator that may appear anywhere in the object.
- The discriminator names are part of the storage and wire format. Never rename them.
- **Required values:** every constructor value is required (`RespectRequiredConstructorParameters`). Bad client input, including a missing `type`, always surfaces as `JsonException` (a 400 in the Api), never a 500.
- **Schema version:** `GameState.SchemaVersion` (currently 3) is read by `GameSerializer.DeserializeState` **before** binding. Older known versions are upgraded on read, and a missing or unknown version throws `NotSupportedException`. Bump it and add the upgrade on breaking changes.
  - **Version 1 → 2:** version 1 had a `nextId` counter. The upgrade drops it and keeps the existing ids; new ids come from the generator.
  - **G10c, no bump:** `Tile.Building` and `City.ProducingBuildingSite` are optional, but `produce_building` now requires `site` and `building_completed` requires `position`, so a stored game log written before G10c with a `building_completed` entry no longer reads. The owner accepted this (no stored games to keep).
  - **Version 2 → 3 (G5, G7):** version 2 kept a stockpile per player (`players[].resources`). The upgrade gives each city, by id, the unowned tiles within the old work radius (1) as territory, and moves each player's stockpile into their first city's stock (a player without a city loses it).

## Tests

`tests/Govern.UnitTests/Core` covers:
- hex math;
- map bounds;
- the RNG;
- the ruleset: the default file parses, and one case per validation rule (`Rulesets/RulesetTests`);
- game setup, including a golden test that the default ruleset gives exactly the games the former constants gave (`State/GameFactoryTests.Create_KnownSeed_GivesTheRecordedState`);
- rules reading changed ruleset values (costs, production, spacing, names, abilities, capture);
- every rule's happy path and errors;
- fog of war, including hidden enemy production and the tile index ↔ coordinate mapping;
- the view previews' parity with `Execute` (`Engine/GameEnginePreviewTests`), including rivers of each kind and bridges around the unit, and the build previews (`Engine/BuildPreviewTests`);
- builders, jobs, yields and buildings (`Actions/BuildImprovementActionTests`, `BuildBridgeActionTests`, `ProduceBuildingActionTests`, `Engine/ConstructionRuleTests`);
- costs and upkeep (`Economy/UpkeepCalculatorTests`, `Engine/UpkeepRuleTests`, `Engine/ProductionCostTests`, `Engine/UpkeepPreviewTests`);
- trading (`Actions/*Deal*`, `*TradeRoute*` and `SetMarketOrderActionTests`, `Engine/TradeRuleTests`, `Economy/TradeCalculatorTests`, `Visibility/TradeRouteViewTests`, `Visibility/TradePreviewCalculatorTests` for parity, scenarios from `Builders/TradeScenario`);
- science (`Actions/UnlockNodeActionTests`, `UpgradeUnitActionTests`, `UpgradeBuildingActionTests`, `Engine/ScienceRuleTests`, the science cases of `Rulesets/RulesetTests`; `GameStateBuilder.WithNodes` gives a player nodes);
- river generation: mirrored, from a source to a mouth, never splitting the passable map (`Map/MapGeneratorTests`);
- JSON round-trips, including a view without tile coordinates;
- a replay determinism check.

`Builders/GameStateBuilder` places units and cities on a 12×10 map for scenario tests. `Builders/TestRulesets` loads the real `rulesets/default/v11.json` (copied next to the tests) and builds variants of it (`TestRulesets.With(json => …)`); `GameStateBuilder.WithTerrain` puts forest, hills, mountains or lake on chosen tiles, `WithRiver` a river on chosen edges, and `WithImprovement`/`WithBridge`/`WithBuilding` an improvement, a bridge or a finished building on a tile; `AddUnit` takes a `UnitJob` and `AddCity` a building in production (with its `buildingSite`), a stock, tiles gained, whether the item is paid, and built buildings (`WithCity` changes a city afterwards); every city claims its `initialRadius` at build (the first city added wins), and `WithTerritory` sets owners by hand.

Architecture tests keep Core pure:
- `LayerDependencyTests.Core_DependsOnlyOnBaseClassLibrary`
- `LayerDependencyTests.Core_DoesNotUseNondeterministicOrIoApis`: no `Random`, clock, `System.IO`/`Net`/`Threading.Tasks`
- `LayerDependencyTests.Core_DoesNotCreateIdentifiersOrUseHashCodes`: no `Guid.NewGuid`/`CreateVersion7`/`GetHashCode(`
- `ProjectReferenceTests.Core_HasNoPackageReferences`
- `FolderStructureTests`: the Core folder whitelist, and each `Actions/XxxAction.cs` holding exactly `XxxCommand` + `XxxRule`
