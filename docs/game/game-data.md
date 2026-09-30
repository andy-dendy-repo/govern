---
title: Game data (the loadable ruleset)
tags: [game, core, ruleset, map-generation, terrain, sight-bonus, config, data, json, catalogs, abilities, texts, localization, validation, versioning, hash]
code: [rulesets, src/Govern.Core/Rulesets, src/Govern.Application/Games/Services/DefaultRulesetService.cs, src/Govern.Client/Features/Games/Services/RulesetService.cs]
updated: 2026-09-30
---

# Game data: the loadable ruleset

**Status: the format is implemented in Core; storing and pinning are deferred** ([ruleset B plan](../plans/deferred/ruleset-b-store-and-pin.md), 2026-09-30). The document below is [`rulesets/default/v2.json`](../../rulesets/default/v2.json) (G4 added terrain and map generation; [`v1.json`](../../rulesets/default/v1.json) stays in the repository unchanged as the published first version, but today's Core no longer reads it, see [Versions in the repository](#versions-in-the-repository)). Core reads, validates and hashes it (`Govern.Core/Rulesets`), and every rule takes its numbers from it: there are no game constants left in Core code. Until rulesets are stored in the database ([ruleset plan](../plans/deferred/ruleset-b-store-and-pin.md), Ruleset B), the Api (`DefaultRulesetService`) and the client (`RulesetService`) embed that file and play every game with it. The algorithms that use this data are in [game logic](game-logic.md).

## The boundary: data or logic

| Goes into the ruleset (data) | Stays in Core (logic) |
|---|---|
| Numbers: costs, movement, sight, spacing, map sizes, production per turn | Algorithms: pathfinding, combat resolution, turn order, victory, visibility |
| Lists and tables: terrains, units, later resources, buildings, tech nodes | The shape of each formula (the data only gives its coefficients) |
| Which abilities a unit has (`found_city`, `attack`) and flags such as `isPassable` | What an ability or flag *does* |
| Names and texts, in every language | Error **codes** (`move.unreachable`), which clients rely on |
| Map generation parameters (terrain weights, spawn distances) | The map generator itself |

**No scripts and no formulas as text.** A ruleset can't contain code or expressions. That keeps games deterministic, safe to load, and easy to validate. A new kind of behaviour always means new Core code plus new data fields.

## Lifecycle

*Steps 1–4 decided (2026-09-30); 5–6 still a proposal. Step 1 and Core's part of step 4 are implemented; the rest comes with Ruleset B, which is deferred: until then new content goes into the embedded file.*

1. **Authoring:** the ruleset is a set of JSON files in the repository (for example `rulesets/default/*.json`), reviewed in PRs like code. Editing in the database (an admin page) may come later.
2. **Storing and publishing:** a migration writes each ruleset version into a `rulesets` table, one row per version (see [The database record](#the-database-record)). An admin role can also publish a version at run time (the role doesn't exist yet). A version is **immutable** once stored and must pass Core's validation; a balance change is a new version.
3. **Pinning:** a new game records the ruleset `key`, `version` and `hash` in its state. A running game always uses its own ruleset, so a balance change never alters a game in progress.
4. **Loading:** the Api reads the ruleset from the database and passes the JSON text to Core. Core parses and validates it (`Ruleset.Parse(json)`), still base class library only, and never touches the database. The Api caches parsed rulesets by `key` and `version`.
5. **Delivering to the client:** `GET /api/rulesets/{key}/{version}` returns the same JSON (public, cacheable by hash). The client needs it for the view previews, names, texts and icons.
6. **Replay:** replaying a game needs its commands, seed and pinned ruleset.

## The database record

*Decided 2026-09-30.* One row per ruleset version in the `rulesets` table. A row never changes once published.

| Column | Type | Meaning |
|---|---|---|
| `id` | `uuid` (v7) | The row id (`BaseEntity`, like every entity) |
| `key` | `text` | The ruleset's lasting text id (`default`), shared by all its versions; `key` + `version` is unique |
| `version` | `int` | 1, 2, 3… within one `key` |
| `name` | `text` | The display name in the lobby and the admin list (`Classic`), per version so a version can rename it; one language for now |
| `hash` | `text` | SHA-256 of the exact `document` text, computed by Core |
| `document` | `text` | The ruleset, stored as the exact text that was published (not `jsonb`, which reorders keys and drops whitespace), so the hash of the text is the same everywhere |
| `created_at` | `timestamptz` | When it was published; no `updated_at`, because a version is immutable |
| `created_by` | `uuid`, nullable | The admin who published it; null when a migration did |

- **The document holds only game content.** `key`, `version` and `name` live only in the row; the migration (or the admin) supplies them, and a repository file's path names them (`rulesets/default/v2.json`).
- **Core and the rulesets are always consistent.** A started game is pinned to one version for good. There's no `schemaVersion` and no upgrading of old rulesets: when a Core change needs new fields, the rulesets are updated with it. Handling older rulesets is deferred until it matters.
- **New games** use the highest `version` of the ruleset. Retiring a version and a lobby description are deferred with Ruleset B ([question 6 there](../plans/deferred/ruleset-b-store-and-pin.md#decisions-and-questions-for-part-b): one ruleset or several).

## Versions in the repository

*Decided 2026-09-30 (G4).* A published version file is never edited: a content change adds `v<N+1>.json` next to it, and `DefaultRulesetService` (Api), `RulesetService` (client) and `TestRulesets` (unit tests) embed or read the newest one. Until Ruleset B pins games to a version, every game, running ones included, is played with the newest file. That is safe as long as a new version keeps every id the old one had (a stored game's tiles and units refer to them). Because Core and the rulesets move together (no `schemaVersion`), an older file may stop parsing when Core adds a required field, as `v1.json` did when `sightBonus` and `map.generation` arrived; it is kept as the record of what was published.

## Format conventions

- **JSON**, one document per ruleset version (optionally split into files per section when authored, merged when stored).
- **Ids** are lower snake_case strings (`settler`, `plains`) and never change once a version is published; texts and references use them.
- **Lists are arrays** of objects with an `id` field (`"units": [ { "id": "settler", … } ]`), not objects keyed by id. The order is meaningful (the order the client lists them in), and the validator rejects duplicate ids.
- **References** are by id (`"producedBy": ["city"]`), and the validator checks each one resolves.
- **Numbers are integers.** Fractions are written as per-mille (`"defenceBonusPerMille": 250` = +25 %), so results are identical on every machine and in the browser.
- **Texts are keys**, resolved from the `texts` section, English only for now (see [Texts and languages](#texts-and-languages)).

## Reading a ruleset in code

| Member (`Govern.Core.Rulesets.Ruleset`) | Use |
|---|---|
| `Ruleset.Parse(json)` | Parses and validates; returns the `Ruleset` or every `RulesetProblem` (`Path`, `Message`) |
| `Ruleset.ParseValid(json)` | For a document known to be valid (an embedded or published one); throws `FormatException` listing every problem |
| `Ruleset.ComputeHash(json)`, `Hash` | SHA-256 of the exact text, lowercase hex; any change, even whitespace, changes it |
| `Unit(type)`, `Terrain(type)`, `FindUnit(type)` | Definitions by id; `FindUnit` returns `null` for an id from an untrusted command |
| `Text(key)`, `UnitName(type)`, `TerrainName(type)`, `CityName(n)` | Texts |
| `Setup`, `Map`, `Terrains`, `Units`, `Cities`, `Texts` | The sections, in the document's order |

Reading is strict, so a typo fails loading instead of silently taking a default: property names are camelCase only, every field is required, unknown fields and duplicate keys are rejected, numbers must be JSON numbers and abilities JSON strings. `UnitType` and `TerrainType` are ids (`readonly record struct`s) written to JSON as the plain id, so a saved game still reads `"settler"` and `"plains"`.

## Top-level structure

```json
{
  "setup": { },
  "map": { },
  "terrains": [ ],
  "units": [ ],
  "cities": { },
  "texts": { }
}
```

## Setup (`setup`)

*Decided 2026-09-30.*

| Field | Default | Meaning |
|---|---|---|
| `players` | 2 | Exactly this many seats in a game; must be 2 for now (later a range, see [F10](../plans/deferred/follow-ups.md#f10-more-than-two-players)) |
| `startingUnits` | settler, soldier | Unit ids each player starts with, in placement order |

```json
"setup": {
  "players": 2,
  "startingUnits": ["settler", "soldier"]
}
```

- **Placement is a Core rule, not data:** the first unit stands on the player's spawn tile (see `map.spawn`); the k-th unit stands k columns from it towards the map centre, on the same row. `["settler", "soldier"]` gives the setup the game always had (`GameFactoryTests.Create_KnownSeed_GivesTheRecordedState`).
- **No `firstTurn`:** the first round is always turn 1; that's how Core counts rounds.
- **Validation:** `startingUnits` isn't empty, every id exists in `units`, at least one of them can found a city (or the player is eliminated on turn 1), and there are no more of them than spawn-zone columns on the smallest map.

## Map (`map`)

| Field | Default | Meaning |
|---|---|---|
| `defaultWidth`, `defaultHeight` | 20 × 14 | The size of a new map (`GameSetup.Width`/`Height` may override it within the limits) |
| `minSize`, `maxSize` | 8, 40 | Allowed width and height; the engine's own bounds are 8–40, which keep a stored game's size bounded |
| `defaultTerrain` | `plains` | Terrain of every tile the generator leaves alone, and of every starting tile; must be passable |
| `spawn.zoneColumnsPerMille` | 250 (a quarter of the width) | Each player spawns in their own zone at the left or right edge; at most 250, so the two zones never meet |
| `spawn.edgeMargin` | 1 tile | Spawns never touch the map edge; at least 1 and less than the zone on the smallest map |

```json
"map": {
  "defaultWidth": 20,
  "defaultHeight": 14,
  "minSize": 8,
  "maxSize": 40,
  "defaultTerrain": "plains",
  "spawn": { "zoneColumnsPerMille": 250, "edgeMargin": 1 },
  "generation": { "patches": [ … ] }
}
```

### Map generation (`map.generation`)

*Decided 2026-09-30 (G4).* The map starts as `defaultTerrain`; each entry of `patches` then lays its terrain down in patches, in the listed order. Every tile is written with its mirror through the map's centre, so both halves of a map are the same. The algorithm is in [game logic](game-logic.md#map-generation).

| Field | Type | Meaning |
|---|---|---|
| `patches[].terrain` | terrain id | The terrain to lay down; not `defaultTerrain`, and each terrain at most once |
| `patches[].sharePerMille` | int ≥ 1 | The share of the map's tiles it aims to cover, in thousandths |
| `patches[].patchSize` | int ≥ 1 | How many tiles each patch grows to when there is room (bigger = fewer, larger patches) |

```json
"generation": {
  "patches": [
    { "terrain": "forest", "sharePerMille": 150, "patchSize": 4 },
    { "terrain": "hills", "sharePerMille": 100, "patchSize": 3 },
    { "terrain": "mountains", "sharePerMille": 40, "patchSize": 2 },
    { "terrain": "lake", "sharePerMille": 40, "patchSize": 3 }
  ]
}
```

- **Limits:** the shares add up to at most 500 (at least half of every map stays default terrain), and the shares of impassable terrains to at most 150, so the generator can always keep the passable tiles connected.
- An empty `patches` list gives an all-default-terrain map, as before G4.

## Terrains (`terrains`)

| Field | Type | Meaning |
|---|---|---|
| `id` | id | `plains` |
| `movementCost` | int ≥ 1 | Movement points to enter the tile (not used while `isPassable` is false, but still required) |
| `isPassable` | bool | Whether land units can enter it |
| `sightBonus` | int ≥ 0 | Extra sight, in tiles, for a unit or city standing on the tile |

```json
"terrains": [
  { "id": "plains", "movementCost": 1, "isPassable": true, "sightBonus": 0 },
  { "id": "forest", "movementCost": 2, "isPassable": true, "sightBonus": 0 },
  { "id": "hills", "movementCost": 2, "isPassable": true, "sightBonus": 1 },
  { "id": "mountains", "movementCost": 1, "isPassable": false, "sightBonus": 0 },
  { "id": "lake", "movementCost": 1, "isPassable": false, "sightBonus": 0 }
]
```

- One flat list for now: a tile has exactly one terrain, so there are no forested hills. Layers (base terrain, elevation, feature) wait until a mechanic needs combinations, likely G5 yields.
- A `sightBonus` only adds sight, so it keeps the fog invariants below. Terrain that blocks sight (forests) is deferred: it would break them.
- The client needs a sprite symbol, two colour tokens and a CSS rule per terrain ([game UI](../features/game-ui.md)).

## Units (`units`)

| Field | Type | Default: settler | Default: soldier |
|---|---|---|---|
| `id` | id | `settler` | `soldier` |
| `movement` | int ≥ 1 | 2 | 2 |
| `sight` | int ≥ 1 | 2 | 2 |
| `productionCost` | int ≥ 1 | 6 | 4 |
| `abilities` | list of abilities | `found_city` | `attack` |

```json
"units": [
  { "id": "settler", "movement": 2, "sight": 2, "productionCost": 6, "abilities": ["found_city"] },
  { "id": "soldier", "movement": 2, "sight": 2, "productionCost": 4, "abilities": ["attack"] }
]
```

**Abilities** (Core's `UnitAbility`; an unknown one fails loading, and none may repeat):

| Ability | What Core does with it |
|---|---|
| `found_city` | Founds a city on its tile and is consumed; a player who has no city and no unit with it is eliminated |
| `attack` | Attacks adjacent enemy units and captures undefended enemy cities |

The order of `units` is the order the client lists them in (the city's Build buttons).

## Cities (`cities`)

| Field | Default | Meaning |
|---|---|---|
| `sight` | 2 | How far a city sees |
| `minimumSpacing` | 3 | The smallest hex distance between two cities |
| `productionPerTurn` | 1 | Production added at the end of the owner's turn |
| `initialProduction` | `soldier` | What a new city builds first |
| `nameTemplate` | `city.default_name` | The text key of a new city's name; the text must contain `{n}`, the founder's city count |
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

Everything a player reads about the game's content comes from here: unit and terrain names and the city name template. Rule-violation messages stay in Core (`Engine/RuleViolations.cs`) for now; moving them here needs the Api to render them per ruleset ([open question 4](design-roadmap.md#open-questions)).

```json
"texts": {
  "unit.settler.name": "Settler",
  "unit.soldier.name": "Soldier",
  "terrain.plains.name": "Plains",
  "terrain.forest.name": "Forest",
  "terrain.hills.name": "Hills",
  "terrain.mountains.name": "Mountains",
  "terrain.lake.name": "Lake",
  "city.default_name": "City {n}"
}
```

- **Decided 2026-09-30:** texts are their own top-level section (not inside `setup`), English only for now, so there is no per-language level. Other languages are deferred.
- The validator requires `unit.{id}.name` for every unit, `terrain.{id}.name` for every terrain and the `nameTemplate` key, and rejects empty texts.
- Core returns rule violation **codes**; the Api or the client looks up the message. *To discuss:* which language the Api uses for the ProblemDetails `title` (the `Accept-Language` header, or always `en` with the client translating).

## Validation (at load time)

`RulesetValidator` rejects a ruleset that breaks any of these and reports every problem with its JSON path (`$.units[1].sight: Must be at least 2, …`); `RulesetTests` has one case per rule:
- The JSON reads strictly (see [Reading a ruleset in code](#reading-a-ruleset-in-code)).
- Ids are lower snake_case (a letter, then letters, digits or `_`, at most 32 characters) and unique per list; `units` and `terrains` are not empty.
- Every reference resolves: `startingUnits`, `initialProduction`, `defaultTerrain`, `map.generation.patches[].terrain`, `nameTemplate`.
- Map generation: a patch terrain is not `defaultTerrain` and not listed twice; `sharePerMille` and `patchSize` ≥ 1; shares add up to at most 500, impassable ones to at most 150.
- `sightBonus` ≥ 0.
- Numbers are in range: movement, sight, costs, city sight, spacing and production ≥ 1; `8 ≤ minSize ≤ maxSize ≤ 40`; the default size within them; `players` = 2.
- Spawn: `zoneColumnsPerMille` 1–250; `edgeMargin` ≥ 1 and less than the zone on the smallest map; `startingUnits` no more than that zone.
- At least one terrain is passable, and `defaultTerrain` is.
- **Fog invariants:** every unit's `sight` ≥ `movement` ÷ the cheapest passable terrain cost, and every unit with `found_city` has `sight` ≥ `cities.minimumSpacing` − 1. The view previews are exact only while these hold.
- At least one starting unit has `found_city`.
- The texts listed in [Texts and languages](#texts-and-languages) exist and aren't empty.

## What stays in Core (not loadable)

- The hex geometry (pointy-top axial, odd-r offset), the six directions and distances.
- The command set and each command's algorithm, turn order, victory and elimination.
- Fog of war: what a player may see, the view shape and its version fingerprint.
- Randomness (`DeterministicRandom`), id generation and the state's JSON schema.
- Error codes (`move.unreachable`, …): clients depend on them.

## Future sections (from the game design roadmap)

Each [roadmap](design-roadmap.md) item adds a section here, with fields, examples and validation rules, before it is implemented:

| Section | Roadmap item |
|---|---|
| `terrains` gains a defence modifier | G9 |
| `terrains` gains elevation and features (layers), yields | G5 |
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

Storing, publishing and serving the ruleset: [ruleset B plan](../plans/deferred/ruleset-b-store-and-pin.md#decisions-and-questions-for-part-b) (deferred). Texts, languages and error messages: [design roadmap open questions](design-roadmap.md#open-questions) 2–4.
