---
title: Ruleset A plan (Core reads a ruleset) (done)
tags: [plan, done, game, core, ruleset, data, json, hash]
code: [rulesets, src/Govern.Core/Rulesets, src/Govern.Application/Games, src/Govern.Client/Features/Games]
updated: 2026-09-30
---

# Plan: The loadable ruleset, part A (done)

**Status:** done (PR #20). Part B (store, pin, serve) is deferred: [ruleset B plan](../deferred/ruleset-b-store-and-pin.md).

## Context
Every game number in `Govern.Core` is a constant or a C# switch today (`UnitCatalog`, `TerrainCatalog`, the `City` constants, `GameSetup`/`GameMap` sizes, the spawn logic in `GameFactory`). We agreed that Core keeps the mechanics and reads every customisable number, list and text from a **ruleset**:
- a JSON document in the repository (`rulesets/default/v1.json`), stored as an immutable version in a `rulesets` table;
- Core parses, validates and hashes it;
- each game is pinned to one version.

The agreed format and database record are in `docs/game/game-data.md`. This is roadmap item G3 (data-driven catalogs). The work is split into two PRs, each a working increment:
- **Ruleset A — Core reads a ruleset.** The engine takes a `Ruleset` everywhere. The Api and the client load the default from the repository file. No database change, and no change in behaviour: the file holds today's values.
- **Ruleset B — store and pin.** The `rulesets` table and its seeding migration. Games are pinned at start through a cached repository. A `GET` endpoint serves a version, and the client fetches the ruleset of the game it shows.

Both branch from `origin/gamecore` (A after PR #19, UnitView, is merged, since both touch the view types) and PR to `gamecore`, through `/develop-feature`.

## The agreed document (v1, today's values)
```json
{
  "setup":   { "players": 2, "startingUnits": ["settler", "soldier"] },
  "map":     { "defaultWidth": 20, "defaultHeight": 14, "minSize": 8, "maxSize": 40, "defaultTerrain": "plains",
               "spawn": { "zoneColumnsPerMille": 250, "edgeMargin": 1 } },
  "terrains": [ { "id": "plains", "movementCost": 1, "isPassable": true } ],
  "units": [
    { "id": "settler", "movement": 2, "sight": 2, "productionCost": 6, "abilities": ["found_city"] },
    { "id": "soldier", "movement": 2, "sight": 2, "productionCost": 4, "abilities": ["attack"] }
  ],
  "cities":  { "sight": 2, "minimumSpacing": 3, "productionPerTurn": 1, "initialProduction": "soldier",
               "nameTemplate": "city.default_name", "captureResetsProduction": true },
  "texts":   { "unit.settler.name": "Settler", "unit.soldier.name": "Soldier", "terrain.plains.name": "Plains",
               "city.default_name": "City {n}" }
}
```
- Decided in this session:
  - an `abilities` list, not flags;
  - no `icon` yet (it comes with G1);
  - texts are English only, in their own section.
- **Rule-violation messages stay in Core** (`RuleViolations`) for now. Moving them into `texts` needs the Api to render them per ruleset, and that belongs with [open question 4 of the design roadmap](../../game/design-roadmap.md#open-questions).

---

# Ruleset A — Core reads a ruleset

## Scope
- In:
  - the Core ruleset model, parser, validator and hash;
  - the engine and every rule take a `Ruleset`;
  - `UnitType` and `TerrainType` become text ids;
  - the catalogs and constants are deleted;
  - the Api and the client load `rulesets/default/v1.json` (embedded);
  - tests, docs and rules.
- Out:
  - the database, pinning and the endpoint (Ruleset B);
  - rule messages as texts;
  - icons;
  - more than 2 players (the validator requires `players == 2` until next follow-ups item 10, because the spawn logic is left or right).

## Affected areas
- **Repository:** new `rulesets/default/v1.json` (the document above).
- **Core, new `Rulesets/` folder:**
  - `Ruleset.cs`: sealed and immutable, holding `Setup`, `Map`, `Terrains`, `Units`, `Cities`, `Texts` and `Hash`. Lookups: `Unit(UnitType)`, `Terrain(TerrainType)` (throw on an unknown id, because that's a bug) and `Text(key)`.
  - `RulesetParser.cs`: `Parse(string json)` returns a `RulesetParseResult` holding a `Ruleset` or problems.
  - `RulesetValidator.cs` and `RulesetProblem.cs`: `(string Path, string Message)`.
  - Section records: `SetupRules`, `MapRules`, `SpawnRules`, `CityRules`.
- **Core, changed:**
  - `Units/UnitType.cs` and `Map/TerrainType.cs` change from enums to `readonly record struct UnitType(string Id)` / `TerrainType(string Id)`. A JSON converter writes a plain string, so the saved-state JSON is unchanged (`"settler"`, `"plains"`).
  - `Units/UnitDefinition.cs` gets `Abilities` plus `CanFoundCity`/`CanAttack` helpers. New `Units/UnitAbility.cs`: an enum written in snake_case (`found_city`, `attack`).
  - `Map/TerrainDefinition.cs` loses the enum.
  - Deleted: `Units/UnitCatalog.cs`, `Map/TerrainCatalog.cs`, the `City` constants, `GameSetup.PlayerCount`/`DefaultWidth`/`DefaultHeight`, and `GameMap.MinSize`/`MaxSize`.
  - `Unit.Definition` and `UnitView.Definition` are removed; callers use `ruleset.Unit(type)`.
  - A `Ruleset` becomes the **first parameter** of:
    - `GameEngine.Execute`, `ViewFor` and the previews (`ReachableTiles`, `AttackTargets`, `CheckFoundCity`);
    - `GameFactory.Create`;
    - `VisibilityCalculator` (sight);
    - `ReachabilityCalculator`/`GameMap.IsPassable` (terrain);
    - `ViewPreviewCalculator`;
    - every `XxxRule.Apply`/`Check`: `EndTurnAction` (cost, production per turn, movement reset), `AttackAction` (attack ability, capture reset), `FoundCityAction` (ability, spacing, initial production, the name from `texts`), `SetProductionAction` (unknown id → `UnknownUnitType`) and `VictoryRule` (founders).
  - `GameSetup(Players, Seed, int? Width = null, int? Height = null)`: null means the ruleset's defaults, and `GameFactory` checks the sizes against `minSize`/`maxSize`. `GameMap.Create(width, height, TerrainType fill)`.
  - `GameFactory` spawn:
    - The zone is `width * zoneColumnsPerMille / 1000`, and the edge margin replaces the hard-coded 1. This gives the same random draws as today, so maps are identical.
    - The k-th starting unit stands k columns towards the centre, on the spawn row.
  - `RuleViolations.CannotFoundCity` message: "This unit can't found a city." (no longer "settlers").
- **Server:**
  - `Govern.Application` embeds `rulesets/default/v1.json` (a linked `EmbeddedResource`).
  - A singleton `Application/Games/Services/DefaultRulesetService.cs` parses it once and throws on problems.
  - `Game.Start(userId, ruleset, now)` and `Game.Execute(ruleset, …)` in Domain.
  - The Start, SendCommand, Get and Watch contracts pass `defaultRulesetService.Ruleset`.
  - `Game.MaxPlayers` stays 2 (a Domain constant, no longer from `GameSetup`).
- **Client:**
  - `Govern.Client` embeds the same file; a singleton `Features/Games/Services/RulesetService.cs` holds the parsed default.
  - `GameTable` takes a `Ruleset` and passes it to the board, the panels and `BoardClickResolver` (previews).
  - `UnitPanel` reads movement and abilities from the ruleset, and shows the name from `texts` ("Your Settler").
  - `CityPanel` has one Build button per `ruleset.Units` in order, with the cost from the ruleset and CSS `produce-{id}`.
  - `BoardPieces` uses the symbol `#{id}` (settler and soldier exist), and `BoardTerrain` the class `terrain-{id}` (`terrain-plains`; update the CSS).
- **Reuse:**
  - SHA-256 hex, as in the view `Version` fingerprint (`Visibility/`);
  - `GameSerializer.Options`' enum converter pattern;
  - `JsonUnmappedMemberHandling.Disallow` so a typo in the document fails loading.

## Steps
1. Add the `rulesets/default/v1.json` file. In Core, add `Rulesets/` (model, parser, validator, hash) next to the old catalogs, and build.
2. In Core, switch `UnitType`/`TerrainType` to ids, thread the `Ruleset` through the engine, rules and calculators, and delete the catalogs and constants. Build.
3. Unit tests compile again: a `TestRulesets.Default` loads the linked file, and `UnitType.Settler` becomes `TestUnits.Settler`. Build.
4. Domain and Application: the embedded default, `DefaultRulesetService`, `Game.Start`/`Execute`, and the contracts. Build.
5. Client: `RulesetService`, `GameTable` and the panels, `BoardPieces`/`BoardTerrain`, CSS. Build.
6. Tests, docs and rules, then `/verify`, review and the PR.

## Validation (in `RulesetValidator`; every problem reported with its JSON path)
- The JSON is valid, no field is missing and there are no unknown fields; ids match `^[a-z][a-z0-9_]{0,31}$` and are unique per list.
- Every reference resolves: `startingUnits`, `initialProduction`, `defaultTerrain` and `nameTemplate`.
- `abilities` are known.
- Numbers are in range: costs, movement, sight and production ≥ 1; `8 ≤ minSize ≤ defaultWidth/defaultHeight ≤ maxSize ≤ 40`; `players == 2`.
- **Spawn:**
  - `1 ≤ edgeMargin < zone columns at minSize`;
  - `zoneColumnsPerMille ≤ 500`;
  - `startingUnits` count ≤ zone columns at minSize, so units stay on the map and on their own half.
- **Fog invariants** (they replace `UnitCatalogTests`):
  - each unit's `sight` ≥ `movement` ÷ the cheapest passable terrain cost;
  - each unit with `found_city` has `sight` ≥ `minimumSpacing` − 1.
- At least one starting unit has `found_city`; `defaultTerrain` is passable.
- `texts` has `unit.{id}.name` for every unit, `terrain.{id}.name` for every terrain, and the `nameTemplate` key, which contains `{n}`.

## Tests
- **Unit:**
  - `Core/Rulesets/RulesetParserTests`: the default file parses. There is one case per validation rule (mutate the default with `JsonNode`, then assert the path and message), an unknown field, and invalid JSON.
  - The hash is the SHA-256 of the exact text, so a whitespace change gives a new hash.
  - `GameFactoryTests`: spawn positions equal today's for fixed seeds (a golden test written before the change); a ruleset with other sizes or starting units is honoured.
  - The action, visibility and preview tests run on `TestRulesets.Default`. Add one case per changed rule where a ruleset value makes a difference: a changed cost, spacing, initial production, the city name from the template, and `captureResetsProduction: false`.
  - `UnitCatalogTests` is deleted; its invariants are now validator cases.
- **Integration:** none new. The game tests stay green, and `GameErrorCodesTests` follows the `CannotFoundCity` message if it asserts it.
- **Client:**
  - `TestViews` and the tests take the default ruleset.
  - `CityPanel` buttons follow the ruleset order.
  - Names come from `texts`: "Enemy Soldier", and "Producing a Settler: 2 of 6".
  - The `terrain-plains` class.
- **Architecture:**
  - `Templates.CoreFolders` gains `Rulesets`.
  - `Client_UsesCoreOnlyForViewsAndPreviews` stays unchanged, since `Ruleset` and the parser are allowed in the client.

## Docs & rules
- `docs/game/game-data.md`: the final v1 document, `abilities`, the validation list, "Implemented (A)".
- `docs/game/game-logic.md`: rules read the ruleset (setup placement, city name).
- `docs/features/game-core.md`: the `Ruleset` API, the parameter on the engine, and the catalogs removed.
- `docs/README.md`.
- `.claude/rules/game-core.md`: the `Rulesets/` folder; "every number comes from the ruleset, never a constant"; the fog invariants are enforced by the validator.
- `.claude/rules/project-map.md`: "a unit or terrain stat" → `rulesets/<key>/vN.json` plus a field in `Core/Rulesets`; the `rulesets/` folder in the repository table; the same in `architecture.md`'s Core template.
- `CLAUDE.md` layout: `rulesets/`.
- `docs/plans/game-design-roadmap.md`: G3 in progress.

## Verification
- `dotnet build Govern.slnx` with 0 warnings; `dotnet format Govern.slnx --verify-no-changes`; `/verify` with a full `dotnet test Govern.slnx` showing `Passed!` (Docker running).
- Mutation checks:
  - break a fog invariant in a copy of the file and see the validator case fail;
  - change `productionPerTurn` in `TestRulesets` and see the EndTurn case fail.
- Manual: run the Api and the client and play a few turns in two windows (Playwright) to check unit names, the build buttons and a founded city named "City 1".
- `code-reviewer` on the diff.
