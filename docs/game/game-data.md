---
title: Game data (the loadable ruleset)
tags: [game, core, ruleset, config, data, json, catalogs, texts, localization, validation, versioning]
code: [src/Govern.Core]
updated: 2026-09-30
---

# Game data: the loadable ruleset

**Status: draft, under discussion.** This describes what should move out of `Govern.Core` code into loadable data. Nothing here is implemented yet: today every value below is a constant or a C# switch in Core. The algorithms that use this data are in [game logic](game-logic.md).

## The boundary: data or logic

| Goes into the ruleset (data) | Stays in Core (logic) |
|---|---|
| Numbers: costs, movement, sight, spacing, map sizes, production per turn | Algorithms: pathfinding, combat resolution, turn order, victory, visibility |
| Lists and tables: terrains, units, later resources, buildings, tech nodes | The shape of each formula (the data only gives its coefficients) |
| Flags that switch a rule on for a type (`canFoundCity`, `canAttack`, `isPassable`) | What a flag *does* |
| Names and texts, in every language | Error **codes** (`move.unreachable`), which clients rely on |
| Map generation parameters (terrain weights, spawn distances) | The map generator itself |

**No scripts and no formulas as text.** A ruleset can't contain code or expressions. That keeps games deterministic, safe to load, and easy to validate. A new kind of behaviour always means new Core code plus new data fields.

## Lifecycle

*Proposal, to discuss:*

1. **Authoring:** the ruleset is a set of JSON files in the repository (for example `rulesets/default/*.json`), reviewed in PRs like code.
2. **Storing:** a migration or seeder writes each ruleset version into a `rulesets` table (`id`, `version`, `hash`, `jsonb` document, `created_at`). A version is **immutable** once stored; a balance change is a new version.
3. **Pinning:** a new game records the ruleset `id`, `version` and `hash` in its state. A running game always uses its own ruleset, so a balance change never alters a game in progress.
4. **Loading:** the Api reads the ruleset from the database and passes the JSON text to Core. Core parses and validates it (`Ruleset.Parse(json)`), still base class library only, and never touches the database. The Api caches parsed rulesets by `id` and `version`.
5. **Delivering to the client:** `GET /api/rulesets/{id}/{version}` returns the same JSON (public, cacheable by hash). The client needs it for the view previews, names, texts and icons.
6. **Replay:** replaying a game needs its commands, seed and pinned ruleset.

## Format conventions

- **JSON**, one document per ruleset version (optionally split into files per section when authored, merged when stored).
- **Ids** are lower snake_case strings (`settler`, `plains`) and never change once a version is published; texts and references use them.
- **References** are by id (`"producedBy": ["city"]`), and the validator checks each one resolves.
- **Numbers are integers.** Fractions are written as per-mille (`"defenceBonusPerMille": 250` = +25 %), so results are identical on every machine and in the browser.
- **Texts are keys**, resolved per language from the `texts` section (see [Texts and languages](#texts-and-languages)).

## Top-level structure

```json
{
  "id": "default",
  "version": 1,
  "schemaVersion": 1,
  "game": { },
  "map": { },
  "terrains": [ ],
  "units": [ ],
  "cities": { },
  "texts": { }
}
```

## Game setup (`game`)

| Field | Today | Meaning |
|---|---|---|
| `players` | 2 (`GameSetup.PlayerCount`) | Seats in a game (later a range, see next follow-ups item 10) |
| `firstTurn` | 1 | The round number at the start |
| `startingUnits` | a settler and a soldier (`GameFactory`) | What each player starts with, and where (see `map.spawn`) |

```json
"game": {
  "players": 2,
  "firstTurn": 1,
  "startingUnits": [
    { "unit": "settler", "place": "spawn" },
    { "unit": "soldier", "place": "next_to_spawn_towards_centre" }
  ]
}
```

## Map (`map`)

| Field | Today | Meaning |
|---|---|---|
| `defaultWidth`, `defaultHeight` | 20 × 14 (`GameSetup`) | The size of a new map |
| `minSize`, `maxSize` | 8, 40 (`GameMap`) | Allowed width and height |
| `defaultTerrain` | `plains` | Terrain of every tile (until map generation, G4) |
| `spawn.zoneColumnsPerMille` | 250 (a quarter of the width) | Each player spawns in their own zone at the left or right edge |
| `spawn.edgeMargin` | 1 tile | Spawns never touch the map edge |

```json
"map": {
  "defaultWidth": 20,
  "defaultHeight": 14,
  "minSize": 8,
  "maxSize": 40,
  "defaultTerrain": "plains",
  "spawn": { "zoneColumnsPerMille": 250, "edgeMargin": 1 }
}
```

## Terrains (`terrains`)

| Field | Type | Meaning |
|---|---|---|
| `id` | id | `plains` |
| `movementCost` | int ≥ 1 | Movement points to enter the tile |
| `isPassable` | bool | Whether land units can enter it |

```json
"terrains": [
  { "id": "plains", "movementCost": 1, "isPassable": true }
]
```

## Units (`units`)

| Field | Type | Today: settler | Today: soldier |
|---|---|---|---|
| `id` | id | `settler` | `soldier` |
| `movement` | int ≥ 1 | 2 | 2 |
| `sight` | int ≥ 1 | 2 | 2 |
| `productionCost` | int ≥ 1 | 6 | 4 |
| `canFoundCity` | bool | true | false |
| `canAttack` | bool | false | true |

```json
"units": [
  { "id": "settler", "movement": 2, "sight": 2, "productionCost": 6, "canFoundCity": true, "canAttack": false },
  { "id": "soldier", "movement": 2, "sight": 2, "productionCost": 4, "canFoundCity": false, "canAttack": true }
]
```

## Cities (`cities`)

| Field | Today | Meaning |
|---|---|---|
| `sight` | 2 (`City.Sight`) | How far a city sees |
| `minimumSpacing` | 3 (`City.MinimumSpacing`) | The smallest hex distance between two cities |
| `productionPerTurn` | 1 (`City.ProductionPerTurn`) | Production added at the end of the owner's turn |
| `initialProduction` | `soldier` | What a new city builds first |
| `nameTemplate` | `City {n}` | The name of a new city, where `{n}` counts the founder's cities (a text key, see below) |
| `captureResetsProduction` | true | A captured city's progress goes back to 0 |

```json
"cities": {
  "sight": 2,
  "minimumSpacing": 3,
  "productionPerTurn": 1,
  "initialProduction": "soldier",
  "nameTemplate": "city.default_name",
  "captureResetsProduction": true
}
```

## Texts and languages

Everything a player reads comes from here: unit, terrain and resource names, city name templates, and the messages for rule violations (today hard-coded in `Engine/RuleViolations.cs`).

```json
"texts": {
  "en": {
    "unit.settler.name": "Settler",
    "unit.soldier.name": "Soldier",
    "terrain.plains.name": "Plains",
    "city.default_name": "City {n}",
    "rule.move.unreachable": "The unit cannot reach that tile this turn.",
    "rule.city.too_close": "Another city is too close."
  },
  "uk": {
    "unit.settler.name": "Поселенець"
  }
}
```

- A missing text falls back to `en`; the validator requires every key in `en`.
- Core returns rule violation **codes**; the Api or the client looks up the message. *To discuss:* which language the Api uses for the ProblemDetails `title` (the `Accept-Language` header, or always `en` with the client translating).

## Validation (at load time)

Core rejects a ruleset that breaks any of these, with the list of every problem:
- Ids are unique within each section; every reference resolves.
- Numbers are in range: costs and movement ≥ 1, sizes within `minSize`–`maxSize`, `defaultWidth`/`defaultHeight` inside them.
- **Fog invariants** (today in `UnitCatalogTests`): every unit's `sight` ≥ its `movement`, and the sight of every unit that can found a city ≥ `cities.minimumSpacing` − 1. The view previews are exact only while these hold.
- `cities.initialProduction` and every `startingUnits` entry name existing units; at least one starting unit can found a city (or the player is eliminated at once).
- Every text key used by the ruleset exists in `en`.

## What stays in Core (not loadable)

- The hex geometry (pointy-top axial, odd-r offset), the six directions and distances.
- The command set and each command's algorithm, turn order, victory and elimination.
- Fog of war: what a player may see, the view shape and its version fingerprint.
- Randomness (`DeterministicRandom`), id generation and the state's JSON schema.
- Error codes (`move.unreachable`, …): clients depend on them.

## Future sections (from the game design roadmap)

Each [roadmap](../plans/game-design-roadmap.md) item adds a section here, with fields, examples and validation rules, before it is implemented:

| Section | Roadmap item |
|---|---|
| `terrains` gains elevation, features, yields, sight and defence modifiers | G4 |
| `resources` (groups, yields, reserves, reveal tech) | G5 |
| `rivers` (states, crossing costs, ship navigation) | G6, G15 |
| `cities` gains territory, growth, housing, loyalty, yields | G7, G8 |
| `units` gains class, health, attacks per turn, resource costs; `combat` counter matrix | G9 |
| `buildings` (cost, health, housing, yields, upgrade chains), builder charges | G10 |
| `techs` and `civics` trees (cost, upkeep, prerequisites, unlocks) | G11 |
| `civilizations` and `governors` (skill trees) | G12, G13 |
| `seasons` (length, per-season modifiers) | G14 |
| `ships` (capacity, water movement) | G15 |

## Open questions

Tracked in [ruleset open questions](../plans/ruleset-open-questions.md) (questions 1–9 there concern this document), to discuss later.
