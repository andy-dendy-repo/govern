---
title: Game data, building and economy sections (yields, territory, bridges, resources, improvements, buildings)
tags: [game, core, ruleset, json, builders, build-production, territory, tile-yields, city-stock, water, desert, culture, bridges, resources, improvements, farm, mine, lumber-camp, buildings, campus, harbour, army-camp, validation]
code: [rulesets/default/v6.json, src/Govern.Core/Rulesets, src/Govern.Core/Economy, src/Govern.Core/Rulesets/EconomyRulesValidator.cs]
updated: 2026-10-01
---

# Game data: building sections of the ruleset

*Decided 2026-10-01 (G10a), first published in [`rulesets/default/v5.json`](../../rulesets/default/v5.json); tile yields and territory (G5, G7) arrived in [`v6.json`](../../rulesets/default/v6.json).* The sections and fields of the ruleset that tile yields, city territory, builders, tile improvements, city buildings and bridges use. The rest of the format (conventions, strict reading, versions, texts) is in [game data](game-data.md); the algorithms are in [game logic](game-logic.md#building-with-units) and the player's view in the [rules reference](rules.md#building-with-units).

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

## Bridges (`bridges`)

| Field | Type | Meaning | Default |
|---|---|---|---|
| `cost` | int 1..1000 | Production a bridge job needs | 6 |

```json
"bridges": { "cost": 6 }
```

## Resources (`resources`)

The resources a city keeps in its stock. Only an id for now; territory growth spends culture, and later sections (costs, upkeep, trade) will spend the rest.

```json
"resources": [ { "id": "food" }, { "id": "water" }, { "id": "wood" }, { "id": "stone" }, { "id": "gold" }, { "id": "science" }, { "id": "culture" } ]
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

Unique per city: a city has each building at most once.

| Field | Type | Meaning |
|---|---|---|
| `id` | id | Needs a `building.{id}.name` text |
| `cost` | int 1..1000 | Production the city needs |
| `yields` | list of `{ "resource", "amount" }` | Added to the city's stock every turn; may be empty |
| `production` | int 0..100 | Added to the city's production per turn (for its own production and the jobs it feeds) |
| `requiresAdjacentTerrain` | list of terrain ids | The city needs one of them next to it (empty: no need); no repeats |

```json
"buildings": [
  { "id": "army_camp", "cost": 8, "yields": [], "production": 1, "requiresAdjacentTerrain": [] },
  { "id": "campus", "cost": 8, "yields": [{ "resource": "science", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "trade_post", "cost": 8, "yields": [{ "resource": "gold", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "culture_centre", "cost": 8, "yields": [{ "resource": "culture", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": [] },
  { "id": "harbour", "cost": 8, "yields": [{ "resource": "food", "amount": 2 }], "production": 0, "requiresAdjacentTerrain": ["lake"] }
]
```

Later items give the buildings their own mechanics (traders from trade posts, faster army production in army camps, ships from harbours), upgrades, health and pillage ([design roadmap](design-roadmap.md), G10b).

## Validation of the building sections

`EconomyRulesValidator` (called by `RulesetValidator`) adds these checks; `RulesetTests` has a case for each:
- `resources` is not empty; `improvements` and `buildings` may be empty; ids are valid and unique per list.
- Costs 1..1000, yields 1..100 per resource, building `production` 0..100, `bridges.cost` 1..1000.
- Every resource and terrain reference resolves; improvement terrains are passable.
- `buildProduction` ≥ 1 exactly for units with `build_improvement` or `build_bridge`.
- Terrain, river and `cities.yields` amounts 1..100 per resource, each resource once; `cities.startingStock` amounts 1..1000.
- `territory`: `initialRadius` 0..10, `maxRadius` `initialRadius`..10, `tileCost` 1..1000, `tileCostIncrease` 0..1000, `growthResource` exists, and `cities.minimumSpacing` > 2 × `initialRadius`, so two new cities never claim the same tile.
- No sight invariant for bridges any more: a unit sees enemy territory on the tiles it sees, and the bridge check only looks at the two tiles beside the edge, which the unit always sees.
- The name texts of every resource, improvement and building exist.
