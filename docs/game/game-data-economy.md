---
title: Game data, building and economy sections (yields, territory, costs, upkeep, bridges, resources, improvements, buildings, trade)
tags: [game, core, ruleset, json, resource-cost, upkeep, supply, building-health, builders, build-production, territory, tile-yields, city-stock, water, desert, culture, bridges, resources, improvements, farm, mine, lumber-camp, buildings, campus, harbour, army-camp, trade, trader, trade-value, market, demand, validation]
code: [rulesets/default/v10.json, src/Govern.Core/Rulesets, src/Govern.Core/Economy, src/Govern.Core/Rulesets/EconomyRulesValidator.cs, src/Govern.Core/Rulesets/UpkeepRulesValidator.cs, src/Govern.Core/Rulesets/TradeRulesValidator.cs]
updated: 2026-10-03
---

# Game data: building sections of the ruleset

*Decided 2026-10-01 (G10a), first published in [`rulesets/default/v5.json`](../../rulesets/default/v5.json); tile yields and territory (G5, G7) arrived in [`v6.json`](../../rulesets/default/v6.json), resource costs, upkeep and building health (G8a) in [`v7.json`](../../rulesets/default/v7.json), trade (G17) in [`v10.json`](../../rulesets/default/v10.json).* The sections and fields of the ruleset that tile yields, city territory, resource costs and upkeep, builders, tile improvements, city buildings, bridges and trade use. The rest of the format (conventions, strict reading, versions, texts) is in [game data](game-data.md); the algorithms are in [game logic](game-logic.md#building-with-units) and the player's view in the [rules reference](rules.md#building-with-units).

## Building fields on units and cities

| Field | Type | Meaning | Default ruleset |
|---|---|---|---|
| `units[].buildProduction` | int 0..100, optional (0 when left out) | Production the unit adds to its job each turn | soldier 1, builder 2, others 0 |
| `units[].abilities`: `build_improvement` | ability | Builds the ruleset's improvements on its own tile in its cities' territory, off the city tiles | builder |
| `units[].abilities`: `build_bridge` | ability | Bridges the river between its tile and a neighbour | soldier, builder |

- A unit has `buildProduction` ≥ 1 exactly when it has `build_improvement` or `build_bridge`; there are no charges, a unit builds as often as it likes.
- Units are listed in the order the client shows them, so a new unit (the builder) goes at the end of `units` and older indexes stay stable.

## Tile yields (`terrains[].yields`, `rivers[].yields`)

| Field | Type | Meaning | Default ruleset |
|---|---|---|---|
| `terrains[].yields` | list of `{ "resource", "amount" }`, may be empty | What a tile of this terrain gives a turn | plains 2 food 1 water, forest 1 food 1 water 2 wood, hills 1 food 2 stone, mountains 2 stone, lake 1 food 3 water, desert nothing |
| `rivers[].yields` | list, may be empty | What a river gives each tile beside it | 1 water for every kind |

- A tile's yield is its terrain's yields, plus, per resource, the **largest** amount any river on its six edges gives (two streams beside a tile still give 1 water), plus its improvement's yields.
- Only tiles in a city's territory give anything to the city ([territory](#territory-territory)).

## City income and stock (`cities.yields`, `cities.startingStock`)

| Field | Type | Meaning | Default ruleset |
|---|---|---|---|
| `cities.yields` | list, may be empty | What every city makes a turn on top of its tiles (the culture that grows territory) | 1 culture |
| `cities.startingStock` | list, amount 1..1000 | The stock a new city starts with | 5 food, 5 water, 5 wood, 5 stone |

Each city keeps its own stock (`City.Stock`). At the end of its owner's turn it adds the yields of every tile in its territory, `cities.yields` and its buildings' yields. A captured city keeps its stock and territory.

## Territory (`territory`)

| Field | Type | Meaning | Default |
|---|---|---|---|
| `initialRadius` | int 0..10 | A new city claims the unowned tiles within this distance | 1 |
| `maxRadius` | int `initialRadius`..10 | A city never claims a tile farther than this | 3 |
| `growthResource` | resource id | What a new tile is paid in | `culture` |
| `tileCost` | int 1..1000 | The price of the first tile gained after founding | 4 |
| `tileCostIncrease` | int 0..1000 | Added to the price for every tile already gained | 2 |

```json
"territory": { "initialRadius": 1, "maxRadius": 3, "growthResource": "culture", "tileCost": 4, "tileCostIncrease": 2 }
```

The k-th tile a city gains costs `tileCost + tileCostIncrease × (k − 1)`. Which tile it takes is a Core rule ([game logic](game-logic.md#territory)).

## Resource costs and upkeep (`resourceCost`, `upkeep`, `upkeep` section)

| Field | Type | Meaning | Default ruleset |
|---|---|---|---|
| `units[].resourceCost`, `buildings[].resourceCost` | list of `{ "resource", "amount" }`, may be empty; amount 1..1000 | Paid in full from the city's stock at the end of the turn production of the item begins; the item waits while the stock can't cover it | settler 4 food 2 water, soldier, builder and trader 2 food, spearman and archer 2 wood, horseman 3 food 2 water; army camp 4 stone 2 wood, campus 4 stone, trade post 4 wood, culture centre 3 stone, harbour 6 wood |
| `units[].upkeep`, `buildings[].upkeep` | list, may be empty; amount 1..100 | Paid every turn by the city that keeps the item up | every unit 1 food 1 water, horseman 2 food 2 water, scout and trader 1 food; army camp 1 food, campus and culture centre 1 gold, trade post and harbour nothing |
| `upkeep.supplyRadius` | int 0..3 | A unit outside its owner's territory is supplied by the land within this distance | 1 (the 7 tiles around it) |
| `upkeep.shortageDamage` | int 1..`combat.maxHealth` − 1 | Health a unit, or a building, loses in a turn its upkeep isn't paid | 10 |
| `upkeep.buildingHealth` | int 1..1000 | A building's full health; at no health it gives no yield and no production | 100 |
| `upkeep.buildingRecovery` | int 1..`buildingHealth` | Health a building regains in a turn its upkeep is paid | 10 |

```json
"upkeep": { "supplyRadius": 1, "shortageDamage": 10, "buildingHealth": 100, "buildingRecovery": 10 }
```

- Bridges and tile improvements have no cost in resources and no upkeep; they are paid with production only.
- A unit never dies of a shortage: its health stops at 1. How a city pays, in which order, and what a unit outside territory takes from the land are Core rules ([game logic](game-logic.md#costs-and-upkeep)).

## Bridges (`bridges`)

| Field | Type | Meaning | Default |
|---|---|---|---|
| `cost` | int 1..1000 | Production a bridge job needs | 6 |

```json
"bridges": { "cost": 6 }
```

## Resources (`resources`)

The resources a city keeps in its stock. Territory growth spends culture, resource costs and upkeep spend the rest, and trade swaps the tradable ones.

| Field | Type | Meaning |
|---|---|---|
| `id` | id | Needs a `resource.{id}.name` text |
| `tradeValue` | int 0..100, optional (0 when left out) | What one unit is worth in trade before a city's demand level; 0 means it can't be traded ([trade](#trade-trade)) |

```json
"resources": [ { "id": "food", "tradeValue": 1 }, { "id": "water" }, { "id": "wood", "tradeValue": 2 }, { "id": "stone", "tradeValue": 2 }, { "id": "gold", "tradeValue": 3 }, { "id": "science" }, { "id": "culture" } ]
```

Each resource needs a `resource.{id}.name` text. The order is the order of a city's stock in the view, the city panel and the top bar.

## Improvements (`improvements`)

| Field | Type | Meaning |
|---|---|---|
| `id` | id | Needs an `improvement.{id}.name` text |
| `terrains` | list of terrain ids | Where it can be built; not empty, no repeats, passable terrains only (the builder stands on the tile) |
| `cost` | int 1..1000 | Production the job needs |
| `yields` | list of `{ "resource", "amount" }` | Added to the tile's yield, so to the stock of the city whose territory holds it; amount 1..100, each resource once |

```json
"improvements": [
  { "id": "farm", "terrains": ["plains"], "cost": 4, "yields": [{ "resource": "food", "amount": 2 }] },
  { "id": "lumber_camp", "terrains": ["forest"], "cost": 4, "yields": [{ "resource": "wood", "amount": 2 }] },
  { "id": "mine", "terrains": ["hills"], "cost": 6, "yields": [{ "resource": "stone", "amount": 2 }] }
]
```

## Buildings (`buildings`)

Unique per city: a city has each building at most once. Each one stands on its own tile of the city's territory, picked by the player when ordering it ([game logic](game-logic.md#produce-a-building)).

| Field | Type | Meaning |
|---|---|---|
| `id` | id | Needs a `building.{id}.name` text |
| `cost` | int 1..1000 | Production the city needs |
| `resourceCost` | list of `{ "resource", "amount" }` | Resources paid when production begins ([costs and upkeep](#resource-costs-and-upkeep-resourcecost-upkeep-upkeep-section)) |
| `upkeep` | list of `{ "resource", "amount" }` | Resources the city pays every turn to keep it |
| `yields` | list of `{ "resource", "amount" }` | Added to the city's stock every turn; may be empty |
| `production` | int 0..100 | Added to the city's production per turn (for its own production and the jobs it feeds) |
| `requiresAdjacentTerrain` | list of terrain ids | The building's tile needs one of them next to it (empty: no need); no repeats |

```json
"buildings": [
  { "id": "army_camp", "cost": 8, "resourceCost": [{ "resource": "stone", "amount": 4 }, { "resource": "wood", "amount": 2 }], "upkeep": [{ "resource": "food", "amount": 1 }], "yields": [], "production": 1, "requiresAdjacentTerrain": [] },
  { "id": "campus", "cost": 8, "resourceCost": [{ "resource": "stone", "amount": 4 }], "upkeep": [{ "resource": "gold", "amount": 1 }], "yields": [{ "resource": "science", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "trade_post", "cost": 8, "resourceCost": [{ "resource": "wood", "amount": 4 }], "upkeep": [], "yields": [{ "resource": "gold", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "culture_centre", "cost": 8, "resourceCost": [{ "resource": "stone", "amount": 3 }], "upkeep": [{ "resource": "gold", "amount": 1 }], "yields": [{ "resource": "culture", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "harbour", "cost": 8, "resourceCost": [{ "resource": "wood", "amount": 6 }], "upkeep": [], "yields": [{ "resource": "food", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": ["lake"] }
]
```

The trade post (`trade.building`) holds its city's market and lets it produce traders ([trade](#trade-trade)). Later items give the other buildings their own mechanics (faster army production in army camps, ships from harbours), upgrades, pillage and repair ([design roadmap](design-roadmap.md), G10b).

## Trade (`trade`)

*Decided 2026-10-03 (G17, the owner's model; not a Civ copy).* How cities swap resources: markets, traders, routes and deals. The algorithms are in [game logic: trading](game-logic-trade.md) and the player's view in the [rules reference](rules.md#trading).

| Field | Type | Meaning | Default |
|---|---|---|---|
| `building` | building id | The building that holds a city's market and lets it produce units with the `trade` ability; a city trades only with it on a tile | `trade_post` |
| `routesPerCity` | int 1..10 | Routes a city may have opened (open or on the way); it can host any number | 2 |
| `maxCap` | int 1..100 | The largest cap of a market order, and the largest amount of a resource in a deal | 20 |
| `demandStepPercent` | int 1..100 | The most a demand level moves in a turn, in percent of itself | 20 |
| `demandSmoothing` | int 1..1000 | Added to need + offer in the demand step, so small markets move slowly | 5 |
| `minDemandPercent` | int 1..100 | The lowest demand level | 25 |
| `maxDemandPercent` | int 100..1000 | The highest demand level | 400 |

```json
"trade": { "building": "trade_post", "routesPerCity": 2, "maxCap": 20, "demandStepPercent": 20, "demandSmoothing": 5, "minDemandPercent": 25, "maxDemandPercent": 400 }
```

- A resource is tradable when its `tradeValue` is at least 1: food, wood, stone and gold. Water, science and culture are not.
- A demand level starts at 100 % (`TradeRules.BaseDemandPercent`, a Core constant) and a city stores only the levels that differ from it.
- The trader is an ordinary unit with the `trade` ability: `civilian`, movement 3, sight 3 (the fog invariant keeps sight ≥ movement), no strength, 6 production, 2 food, upkeep 1 food.

## Validation of the building sections

`EconomyRulesValidator` and `UpkeepRulesValidator` (called by `RulesetValidator`) add these checks; `RulesetTests` has a case for each:
- `resources` is not empty; `improvements` and `buildings` may be empty; ids are valid and unique per list.
- Costs 1..1000, yields 1..100 per resource, building `production` 0..100, `bridges.cost` 1..1000.
- Every resource and terrain reference resolves; improvement terrains are passable.
- `buildProduction` ≥ 1 exactly for units with `build_improvement` or `build_bridge`.
- Terrain, river and `cities.yields` amounts 1..100 per resource, each resource once; `cities.startingStock` amounts 1..1000.
- `territory`: `initialRadius` 0..10, `maxRadius` `initialRadius`..10, `tileCost` 1..1000, `tileCostIncrease` 0..1000, `growthResource` exists, and `cities.minimumSpacing` > 2 × `initialRadius`, so two new cities never claim the same tile.
- No sight invariant for bridges any more: a unit sees enemy territory on the tiles it sees, and the bridge check only looks at the two tiles beside the edge, which the unit always sees.
- Resource costs: amounts 1..1000, each resource once, and never `territory.growthResource` (territory growth spends it just before production pays, so the city panel could not tell for sure whether production can begin). Upkeep: amounts 1..100, each resource once.
- `upkeep`: `supplyRadius` 0..3, `shortageDamage` 1..`combat.maxHealth` − 1, `buildingHealth` 1..1000, `buildingRecovery` 1..`buildingHealth`.
- **Fog invariant:** a unit with upkeep has `sight` ≥ `upkeep.supplyRadius` + 1, so it sees every tile that supplies it and the rivers on their edges, and the supply preview is exact.
- Trade (`TradeRulesValidator`): `tradeValue` 0..100 and 0 for `territory.growthResource` (trade happens just before territory growth, so the city panel's price of the next tile stays exact); at least one tradable resource; a unit with the `trade` ability has no other ability (a trader on a route takes no other order); `trade.building` exists; the ranges in [the table](#trade-trade).
- The name texts of every resource, improvement and building exist.
