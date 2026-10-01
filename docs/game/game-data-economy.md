---
title: Game data, building sections (bridges, resources, improvements, buildings)
tags: [game, core, ruleset, json, builders, build-production, work-area, bridges, resources, stockpile, improvements, farm, mine, lumber-camp, buildings, campus, harbour, army-camp, validation]
code: [rulesets/default/v5.json, src/Govern.Core/Rulesets, src/Govern.Core/Economy, src/Govern.Core/Rulesets/EconomyRulesValidator.cs]
updated: 2026-10-01
---

# Game data: building sections of the ruleset

*Decided 2026-10-01 (G10a), first published in [`rulesets/default/v5.json`](../../rulesets/default/v5.json).* The sections and fields of the ruleset that builders, tile improvements, city buildings and bridges use. The rest of the format (conventions, strict reading, versions, texts) is in [game data](game-data.md); the algorithms are in [game logic](game-logic.md#building-with-units) and the player's view in the [rules reference](rules.md#building-with-units).

## Building fields on units and cities

| Field | Type | Meaning | Default ruleset |
|---|---|---|---|
| `units[].buildProduction` | int 0..100, optional (0 when left out) | Production the unit adds to its job each turn | soldier 1, builder 2, others 0 |
| `units[].abilities`: `build_improvement` | ability | Builds the ruleset's improvements on its own tile in an own work area | builder |
| `units[].abilities`: `build_bridge` | ability | Bridges the river between its tile and a neighbour | soldier, builder |
| `cities.workRadius` | int 1..5 | How far a city's work area reaches (the tiles at distance 1..radius; the city's own tile counts as its ground but holds no improvement) | 1 |

- A unit has `buildProduction` ≥ 1 exactly when it has `build_improvement` or `build_bridge`; there are no charges, a unit builds as often as it likes.
- Units are listed in the order the client shows them, so a new unit (the builder) goes at the end of `units` and older indexes stay stable.

## Bridges (`bridges`)

| Field | Type | Meaning | Default |
|---|---|---|---|
| `cost` | int 1..1000 | Production a bridge job needs | 6 |

```json
"bridges": { "cost": 6 }
```

## Resources (`resources`)

The resources a player stockpiles. Only an id for now; later sections (trade, upkeep) will spend them.

```json
"resources": [ { "id": "food" }, { "id": "wood" }, { "id": "stone" }, { "id": "gold" }, { "id": "science" }, { "id": "culture" } ]
```

Each resource needs a `resource.{id}.name` text. The order is the order of the stockpile in the view and in the top bar.

## Improvements (`improvements`)

| Field | Type | Meaning |
|---|---|---|
| `id` | id | Needs an `improvement.{id}.name` text |
| `terrains` | list of terrain ids | Where it can be built; not empty, no repeats, passable terrains only (the builder stands on the tile) |
| `cost` | int 1..1000 | Production the job needs |
| `yields` | list of `{ "resource", "amount" }` | Added to the owner's stockpile every turn while the tile is in one of their work areas; amount 1..100, each resource once |

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
| `yields` | list of `{ "resource", "amount" }` | Added to the stockpile every turn; may be empty |
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
- **Fog invariant:** a unit with `build_bridge` has `sight` ≥ `cities.workRadius` + 1, so it sees every city whose work area could touch a bridge it builds, and the bridge preview stays exact.
- `cities.workRadius` is 1..5 and less than half of `cities.minimumSpacing`, so two cities' work areas never overlap, and at most `cities.sight`, so a city sees the improvements in its work area (they show on visible tiles only).
- The name texts of every resource, improvement and building exist.
