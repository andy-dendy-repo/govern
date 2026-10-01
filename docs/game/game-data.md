---
title: Game data (the loadable ruleset)
tags: [game, core, ruleset, map-generation, terrain, rivers, sight-bonus, config, data, json, catalogs, abilities, texts, localization, validation, versioning, hash]
code: [rulesets, src/Govern.Core/Rulesets, src/Govern.Application/Games/Services/DefaultRulesetService.cs, src/Govern.Client/Features/Games/Services/RulesetService.cs]
updated: 2026-10-01
---

# Game data: the loadable ruleset

**Status: the format is implemented in Core; storing and pinning are deferred** ([ruleset B plan](../plans/deferred/ruleset-b-store-and-pin.md), 2026-09-30). The document below is [`rulesets/default/v4.json`](../../rulesets/default/v4.json) (G4 added terrain and map generation in v2, G9 units, health and combat in v3, G6 rivers in v4; [`v1.json`](../../rulesets/default/v1.json), [`v2.json`](../../rulesets/default/v2.json) and [`v3.json`](../../rulesets/default/v3.json) stay in the repository unchanged as published versions, but today's Core no longer reads them, see [Versions in the repository](#versions-in-the-repository)). Core reads, validates and hashes it (`Govern.Core/Rulesets`), and every rule takes its numbers from it: there are no game constants left in Core code. Until rulesets are stored in the database ([ruleset plan](../plans/deferred/ruleset-b-store-and-pin.md), Ruleset B), the Api (`DefaultRulesetService`) and the client (`RulesetService`) embed that file and play every game with it. The algorithms that use this data are in [game logic](game-logic.md).

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

- **The document holds only game content.** `key`, `version` and `name` live only in the row; the migration (or the admin) supplies them, and a repository file's path names them (`rulesets/default/v4.json`).
- **Core and the rulesets are always consistent.** A started game is pinned to one version for good. There's no `schemaVersion` and no upgrading of old rulesets: when a Core change needs new fields, the rulesets are updated with it. Handling older rulesets is deferred until it matters.
- **New games** use the highest `version` of the ruleset. Retiring a version and a lobby description are deferred with Ruleset B ([question 6 there](../plans/deferred/ruleset-b-store-and-pin.md#decisions-and-questions-for-part-b): one ruleset or several).

## Versions in the repository

*Decided 2026-09-30 (G4).* A published version file is never edited: a content change adds `v<N+1>.json` next to it, and `DefaultRulesetService` (Api), `RulesetService` (client) and `TestRulesets` (unit tests) embed or read the newest one. Until Ruleset B pins games to a version, every game, running ones included, is played with the newest file. That is safe as long as a new version keeps every id the old one had (a stored game's tiles and units refer to them). Because Core and the rulesets move together (no `schemaVersion`), an older file may stop parsing when Core adds a required field, as `v1.json` did when `sightBonus` and `map.generation` arrived (G4) `v2.json` when `class`, `defenceBonus` and `combat` arrived (G9), and `v3.json` when `rivers` and `map.generation.rivers` arrived (G6); they are kept as the record of what was published. A unit stores the damage it has taken, not its health, so until games are pinned a new version must not lower `combat.maxHealth` (a hurt unit could be left with no health).

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
| `Text(key)`, `UnitName(type)`, `TerrainName(type)`, `ClassName(class)`, `CityName(n)` | Texts |
| `Setup`, `Map`, `Terrains`, `Units`, `Cities`, `Combat`, `Texts` | The sections, in the document's order |

Reading is strict, so a typo fails loading instead of silently taking a default: property names are camelCase only, every field is required (except a unit's `strength` and `ranged`, whose absence means "can't fight" and "no ranged attack"), unknown fields and duplicate keys are rejected, numbers must be JSON numbers and abilities JSON strings. `UnitType`, `TerrainType` and `RiverType` are ids (`readonly record struct`s) written to JSON as the plain id, so a saved game still reads `"settler"`, `"plains"` and `"river"`.

## Top-level structure

```json
{
  "setup": { },
  "map": { },
  "terrains": [ ],
  "rivers": [ ],
  "units": [ ],
  "cities": { },
  "combat": { },
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
  "generation": { "patches": [ … ], "rivers": { … } }
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

### River generation (`map.generation.rivers`)

*Decided 2026-10-01 (G6).* After the terrain, the generator lays down rivers along tile edges. Each river starts at a corner of a `sources` tile, runs along edges that share a corner, never back towards its source, and ends at the first corner that touches a `mouths` tile or the map's border. Each river is laid with its mirror. The algorithm is in [game logic](game-logic.md#rivers).

| Field | Type | Meaning |
|---|---|---|
| `count` | int 0..20 | Rivers to lay down, each with its mirror (0 for none). Fewer when the map has no room |
| `minLength`, `maxLength` | int 1..100, min ≤ max | Edges per river; a river that hasn't reached a mouth by `maxLength` is dropped, as is one shorter than `minLength` |
| `sources` | terrain ids | Terrains a river may start next to; not empty, each once |
| `mouths` | terrain ids | Terrains a river may end at (the map's border always ends one); not empty, each once |
| `stages[].river` | river id | The river kind of this stretch |
| `stages[].fromSegment` | int | The edge number, counted from the source (0), where the stretch starts; the first is 0, then strictly increasing |

```json
"rivers": {
  "count": 2,
  "minLength": 4,
  "maxLength": 12,
  "sources": ["hills", "mountains"],
  "mouths": ["lake"],
  "stages": [
    { "river": "stream", "fromSegment": 0 },
    { "river": "river", "fromSegment": 3 },
    { "river": "deep_river", "fromSegment": 7 }
  ]
}
```

- The first stage must be a crossable river: where an uncrossable stretch would cut the passable tiles in two, the generator lays the last crossable kind before it instead, so every passable tile stays reachable.

## Terrains (`terrains`)

| Field | Type | Meaning |
|---|---|---|
| `id` | id | `plains` |
| `movementCost` | int ≥ 1 | Movement points to enter the tile (not used while `isPassable` is false, but still required) |
| `isPassable` | bool | Whether land units can enter it |
| `sightBonus` | int ≥ 0 | Extra sight, in tiles, for a unit or city standing on the tile |
| `defenceBonus` | int 0..100 | Extra strength for a unit defending on the tile (G9) |

```json
"terrains": [
  { "id": "plains", "movementCost": 1, "isPassable": true, "sightBonus": 0, "defenceBonus": 0 },
  { "id": "forest", "movementCost": 2, "isPassable": true, "sightBonus": 0, "defenceBonus": 3 },
  { "id": "hills", "movementCost": 2, "isPassable": true, "sightBonus": 1, "defenceBonus": 3 },
  { "id": "mountains", "movementCost": 1, "isPassable": false, "sightBonus": 0, "defenceBonus": 0 },
  { "id": "lake", "movementCost": 1, "isPassable": false, "sightBonus": 0, "defenceBonus": 0 }
]
```

- One flat list for now: a tile has exactly one terrain, so there are no forested hills. Layers (base terrain, elevation, feature) wait until a mechanic needs combinations, likely G5 yields.
- A `sightBonus` only adds sight, so it keeps the fog invariants below. Terrain that blocks sight (forests) is deferred: it would break them.
- A `defenceBonus` counts only for the defender, and is a whole number of strength points, not a percentage (strengths are small integers).
- The client needs a sprite symbol, two colour tokens and a CSS rule per terrain ([game UI](../features/game-ui.md)).

## Rivers (`rivers`)

*Decided 2026-10-01 (G6).* Rivers run along the edges between tiles. Each tile stores the rivers on its east, north-east and north-west edges (`Tile.Rivers`), so every edge is stored once; a tile without rivers leaves the field out of the JSON, so games saved before G6 read unchanged.

| Field | Type | Meaning |
|---|---|---|
| `id` | id | `river` |
| `crossingCost` | int 0..100 | Movement points a land unit spends to cross, on top of the cost of the tile it enters |
| `isCrossable` | bool | Whether land units may cross at all (false: a deep river, crossed later by a bridge, G10) |
| `defenceBonus` | int 0..100 | Extra strength for a unit attacked in melee across it |

```json
"rivers": [
  { "id": "stream", "crossingCost": 0, "isCrossable": true, "defenceBonus": 0 },
  { "id": "river", "crossingCost": 1, "isCrossable": true, "defenceBonus": 3 },
  { "id": "deep_river", "crossingCost": 0, "isCrossable": false, "defenceBonus": 5 }
]
```

- At least one kind is crossable. A `crossingCost` only adds, so it keeps the fog invariants below.
- Ranged attacks ignore rivers; a melee unit may attack a unit across an uncrossable river but not capture a city across one.
- Bridges (G10), ship navigation (G15) and seasons changing a river's kind (G14) add fields when they arrive.
- The client needs a CSS rule per river (`.river-<id>`, its width) in `board.css` ([game UI](../features/game-ui.md)); `GameUiTests.BoardCss_StylesEveryRiver` checks it.

## Units (`units`)

| Field | Type | Meaning | Default: soldier |
|---|---|---|---|
| `id` | id | | `soldier` |
| `class` | id | The combat class `combat.counters` refer to; free ids (`civilian`, `melee`, `anti_cavalry`, `cavalry`, `ranged`), each with a `class.{id}.name` text | `melee` |
| `movement` | int ≥ 1 | Movement points per turn | 2 |
| `sight` | int ≥ 1 | Sight radius | 2 |
| `productionCost` | int ≥ 1 | Production a city needs to build it | 4 |
| `strength` | int 1..1000, optional | Strength in melee and when attacked; left out for a civilian, which any attack destroys | 20 |
| `ranged` | object, optional | `{ "strength": 1..1000, "range": 1..sight }`; set exactly when the unit has `ranged_attack` | — |
| `abilities` | list of abilities | What it can do | `attack` |

```json
"units": [
  { "id": "settler", "class": "civilian", "movement": 2, "sight": 2, "productionCost": 6, "abilities": ["found_city"] },
  { "id": "soldier", "class": "melee", "movement": 2, "sight": 2, "productionCost": 4, "strength": 20, "abilities": ["attack"] },
  { "id": "spearman", "class": "anti_cavalry", "movement": 2, "sight": 2, "productionCost": 4, "strength": 18, "abilities": ["attack"] },
  { "id": "horseman", "class": "cavalry", "movement": 3, "sight": 3, "productionCost": 6, "strength": 20, "abilities": ["attack"] },
  { "id": "archer", "class": "ranged", "movement": 2, "sight": 2, "productionCost": 5, "strength": 10, "ranged": { "strength": 22, "range": 2 }, "abilities": ["ranged_attack"] }
]
```

**Abilities** (Core's `UnitAbility`; an unknown one fails loading, and none may repeat):

| Ability | What Core does with it |
|---|---|
| `found_city` | Founds a city on its tile and is consumed; a player who has no city and no unit with it is eliminated |
| `attack` | Attacks adjacent enemy units in melee (both take damage) and captures undefended enemy cities |
| `ranged_attack` | Shoots an enemy unit within `ranged.range` (no damage back); can't capture a city |

- A unit with `attack` or `ranged_attack` needs a `strength`; a unit can't have both attacks.
- `ranged.range` ≤ `sight`, so every target in range is visible and the attack preview stays exact.
- The order of `units` is the order the client lists them in (the city's Build buttons).

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

## Combat (`combat`)

*Decided 2026-09-30 (G9).* The formula is in [game logic](game-logic.md#combat); every number is here.

| Field | Default | Meaning |
|---|---|---|
| `maxHealth` | 100 | Every unit's full health; 0 means destroyed |
| `damage.base` | 30 | Damage between equally strong units (within `min`..`max`) |
| `damage.perStrengthPoint` | 2 | Extra damage per point the dealer is stronger (less when weaker); ≥ 0 |
| `damage.min`, `damage.max` | 5, 100 | The damage before the roll is kept within these (`1 ≤ min ≤ max`) |
| `damage.spreadPercent` | 20 | The roll scales the damage by 100 ± this percent (0..99) |
| `healing.perTurn` | 10 | Health a hurt unit that rested (neither moved nor attacked) heals at the end of its owner's turn |
| `healing.inOwnCity` | 20 | The same, in one of its owner's cities |
| `counters` | 3 entries | `{ "class", "against", "bonus" }`: units of `class` get `bonus` strength in any fight against units of `against`, attacking or defending |

```json
"combat": {
  "maxHealth": 100,
  "damage": { "base": 30, "perStrengthPoint": 2, "min": 5, "max": 100, "spreadPercent": 20 },
  "healing": { "perTurn": 10, "inOwnCity": 20 },
  "counters": [
    { "class": "melee", "against": "anti_cavalry", "bonus": 5 },
    { "class": "anti_cavalry", "against": "cavalry", "bonus": 10 },
    { "class": "cavalry", "against": "ranged", "bonus": 5 }
  ]
}
```

- **Validation:** `maxHealth` 1..1000; `1 ≤ min ≤ base ≤ max ≤ 1000`; `perStrengthPoint` 0..100; `spreadPercent` 0..99; healing 0..1000; each counter names classes some unit has, a bonus 1..100, and no class pair twice. Unit strengths are 1..1000 and terrain `defenceBonus` 0..100. The upper bounds keep the integer combat maths far from overflowing.

## Texts and languages

Everything a player reads about the game's content comes from here: unit, class and terrain names and the city name template. Rule-violation messages stay in Core (`Engine/RuleViolations.cs`) for now; moving them here needs the Api to render them per ruleset ([open question 4](design-roadmap.md#open-questions)).

```json
"texts": {
  "unit.settler.name": "Settler",
  "unit.soldier.name": "Soldier",
  "unit.spearman.name": "Spearman",
  "unit.horseman.name": "Horseman",
  "unit.archer.name": "Archer",
  "class.civilian.name": "Civilian",
  "class.melee.name": "Melee",
  "class.anti_cavalry.name": "Anti-cavalry",
  "class.cavalry.name": "Cavalry",
  "class.ranged.name": "Ranged",
  "terrain.plains.name": "Plains",
  "terrain.forest.name": "Forest",
  "terrain.hills.name": "Hills",
  "terrain.mountains.name": "Mountains",
  "terrain.lake.name": "Lake",
  "city.default_name": "City {n}"
}
```

- **Decided 2026-09-30:** texts are their own top-level section (not inside `setup`), English only for now, so there is no per-language level. Other languages are deferred.
- The validator requires `unit.{id}.name` for every unit, `class.{id}.name` for every unit class, `terrain.{id}.name` for every terrain, `river.{id}.name` for every river and the `nameTemplate` key, and rejects empty texts.
- Core returns rule violation **codes**; the Api or the client looks up the message. *To discuss:* which language the Api uses for the ProblemDetails `title` (the `Accept-Language` header, or always `en` with the client translating).

## Validation (at load time)

`RulesetValidator` rejects a ruleset that breaks any of these and reports every problem with its JSON path (`$.units[1].sight: Must be at least 2, …`); `RulesetTests` has one case per rule:
- The JSON reads strictly (see [Reading a ruleset in code](#reading-a-ruleset-in-code)).
- Ids are lower snake_case (a letter, then letters, digits or `_`, at most 32 characters) and unique per list; `units`, `terrains` and `rivers` are not empty.
- Every reference resolves: `startingUnits`, `initialProduction`, `defaultTerrain`, `map.generation.patches[].terrain`, `map.generation.rivers` `sources`, `mouths` and `stages[].river`, `nameTemplate`.
- Map generation: a patch terrain is not `defaultTerrain` and not listed twice; `sharePerMille` and `patchSize` ≥ 1; shares add up to at most 500, impassable ones to at most 150.
- `sightBonus` ≥ 0; `defenceBonus` 0..100.
- Rivers: `crossingCost` and `defenceBonus` 0..100; at least one kind crossable; generation `count` 0..20, `1 ≤ minLength ≤ maxLength ≤ 100`, `sources` and `mouths` not empty and without repeats, `stages` not empty, starting at 0, strictly increasing, the first one crossable.
- Units and combat: see [Units](#units-units) and [Combat](#combat-combat).
- Numbers are in range: movement, sight, costs, city sight, spacing and production ≥ 1; `8 ≤ minSize ≤ maxSize ≤ 40`; the default size within them; `players` = 2.
- Spawn: `zoneColumnsPerMille` 1–250; `edgeMargin` ≥ 1 and less than the zone on the smallest map; `startingUnits` no more than that zone.
- At least one terrain is passable, and `defaultTerrain` is.
- **Fog invariants:** every unit's `sight` ≥ `movement` ÷ the cheapest passable terrain cost, every unit with `found_city` has `sight` ≥ `cities.minimumSpacing` − 1, and a ranged unit's `range` ≤ `sight`. The view previews are exact only while these hold.
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
| `terrains` gains elevation and features (layers), yields | G5 |
| `resources` (groups, yields, reserves, reveal tech) | G5 |
| `rivers` gains bridges (G10), ship navigation (G15), seasonal kinds (G14) | G10, G15, G14 |
| `cities` gains territory, growth, housing, loyalty, yields | G7, G8 |
| `units` gains attacks per turn (deferred from G9 until a unit differs), resource costs (G5) | G9, G5 |
| `buildings` (cost, health, housing, yields, upgrade chains), builder charges | G10 |
| `techs` and `civics` trees (cost, upkeep, prerequisites, unlocks) | G11 |
| `civilizations` and `governors` (skill trees) | G12, G13 |
| `seasons` (length, per-season modifiers) | G14 |
| `ships` (capacity, water movement) | G15 |

## Open questions

Storing, publishing and serving the ruleset: [ruleset B plan](../plans/deferred/ruleset-b-store-and-pin.md#decisions-and-questions-for-part-b) (deferred). Texts, languages and error messages: [design roadmap open questions](design-roadmap.md#open-questions) 2–4.
