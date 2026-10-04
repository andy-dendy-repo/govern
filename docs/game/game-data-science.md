---
title: "Game data: the science section (science tree, unit lines, building upgrades)"
tags: [game, ruleset, json, science, science-tree, nodes, points, requires, unlocks, replaces, upgradeCost, upgrades, unit-upgrade, building-upgrade, validation]
code: [rulesets/default/v11.json, src/Govern.Core/Rulesets/ScienceRules.cs, src/Govern.Core/Rulesets/ScienceRulesValidator.cs, src/Govern.Core/Science]
updated: 2026-10-04
---

# Game data: the science section

The science tree (G11a, ruleset v11) as data: the `science` section, `units[].replaces` and `units[].upgradeCost`, and `buildings[].upgrades`. The rest of the ruleset is in [game data](game-data.md) and [game data: building sections](game-data-economy.md); the algorithms are in [game logic](game-logic.md#science); the player rules are in [rules](rules.md#science).

## Science (`science`)

Science is a budget per turn, never a stock: a player's science per turn is the amount of `science.resource` in all their cities' income now (`cities.yields` plus building yields, so 1 per city plus a campus's 2 or a university's 4 in v11). Every enabled node a player owns takes its `points` of it every turn.

| Field | Type | Meaning | Default |
|---|---|---|---|
| `resource` | resource id | The resource counted as science | `science` |
| `nodes` | list | The tree's nodes, in the order the science tree lists them; an empty list means no tree and nothing locked | 11 nodes |
| `nodes[].id` | id | The node's id; its name is the `science.{id}.name` text | `archery` |
| `nodes[].points` | int 1..100 | Science the node takes every turn while it is enabled | 1 |
| `nodes[].requires` | list of node ids | Nodes that must be owned and enabled to unlock it, and to switch it back on; each listed before this node | `[]` |
| `nodes[].units` | list of unit ids | Units locked until this node is owned and enabled | `["archer"]` |
| `nodes[].buildings` | list of building ids | Buildings locked until this node is owned and enabled | `[]` |

```json
"science": {
  "resource": "science",
  "nodes": [
    { "id": "archery", "points": 1, "requires": [], "units": ["archer"], "buildings": [] },
    { "id": "bronze_working", "points": 1, "requires": [], "units": ["swordsman"], "buildings": [] },
    { "id": "education", "points": 2, "requires": [], "units": [], "buildings": ["university"] },
    { "id": "iron_working", "points": 2, "requires": ["bronze_working"], "units": ["pikeman"], "buildings": ["barracks"] }
  ]
}
```

The default tree (v11):

| Node | Points | Requires | Unlocks |
|---|---|---|---|
| `archery` | 1 | — | archer |
| `horseback_riding` | 1 | — | horseman |
| `trade` | 1 | — | trader, trade_post |
| `sailing` | 1 | — | harbour |
| `bronze_working` | 1 | — | swordsman |
| `education` | 2 | — | university |
| `iron_working` | 2 | bronze_working | pikeman, barracks |
| `machinery` | 2 | archery | crossbowman, workshop |
| `stirrups` | 2 | horseback_riding | knight |
| `currency` | 2 | trade | market |
| `shipbuilding` | 2 | sailing | port |

## Unit lines (`units[].replaces`, `units[].upgradeCost`)

| Field | Type | Meaning | Default: swordsman |
|---|---|---|---|
| `replaces` | unit id, optional | The older unit of the same class this one replaces: once this one is unlocked, cities no longer start the older one, and an existing older unit can be upgraded to it | `soldier` |
| `upgradeCost` | list of `{ "resource", "amount" }`, set exactly with `replaces` | What the city owning the unit's tile pays to upgrade an older unit of the line into this one (may be empty) | 2 stone |

```json
{ "id": "swordsman", "class": "melee", "movement": 2, "sight": 2, "productionCost": 6, "strength": 26, "buildProduction": 1,
  "resourceCost": [{ "resource": "food", "amount": 2 }, { "resource": "stone", "amount": 2 }],
  "upkeep": [{ "resource": "food", "amount": 1 }, { "resource": "water", "amount": 1 }],
  "abilities": ["attack", "build_bridge"], "replaces": "soldier", "upgradeCost": [{ "resource": "stone", "amount": 2 }] }
```

A line can be longer than two units: the newest unlocked unit along the `replaces` links is what cities produce and what an upgrade turns a unit into. v11 has four lines: soldier → swordsman, spearman → pikeman, archer → crossbowman, horseman → knight.

## Building upgrades (`buildings[].upgrades`)

| Field | Type | Meaning | Default: university |
|---|---|---|---|
| `upgrades` | building id, optional | The building this one upgrades. A city never produces an upgrade: a builder's job turns the old building into it on its tile. Its `cost` is the job's production, its `resourceCost` is paid by the city when the job is done | `campus` |

v11 upgrades: army_camp → barracks (production 2), campus → university (science 4), trade_post → market (gold 4, still the city's market), harbour → port (food 4, needs a lake next to it). The workshop (production 1) is a new building, not an upgrade.

## Validation of the science section

`ScienceRulesValidator` (called by `RulesetValidator`) reports, with JSON paths:
- `science.resource` exists, isn't tradable, and is in no `resourceCost`, `upkeep`, `upgradeCost`, `cities.startingStock` or `territory.growthResource` (science is never stored or paid).
- Node ids are valid and unique; `points` is 1..100; every `requires` names a node listed earlier (so the tree has no loops); every node has its `science.{id}.name` text.
- Every unit and building a node lists exists, and each is listed by at most one node.
- No node locks a `setup.startingUnits` unit or `cities.initialProduction`.
- `replaces` names another unit of the same class, at most one unit replaces each unit, and the `replaces` links have no loop; `upgradeCost` is set exactly when `replaces` is, with valid amounts.
- `upgrades` names another building, at most one building upgrades each building, and the `upgrades` links have no loop; `trade.building` is not an upgrade (a city must be able to produce it).
