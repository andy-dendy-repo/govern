---
title: Ruleset delivery plan (A: Core reads a ruleset, B: store, pin, serve)
tags: [plan, game, core, ruleset, data, json, hash, migration, caching, pinning]
code: [rulesets, src/Govern.Core/Rulesets, src/Govern.Application/Games, src/Govern.Client/Features/Games]
updated: 2026-09-30
---

# Plan: The loadable ruleset (two PRs)

**Status:** Ruleset A implemented (branch `feature/ruleset-core`); Ruleset B next.

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
- **Rule-violation messages stay in Core** (`RuleViolations`) for now. Moving them into `texts` needs the Api to render them per ruleset, and that belongs with open question 9.

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

---

# Ruleset B — store, pin, serve

## Scope
- In:
  - the `rulesets` table and a migration seeding default v1;
  - `games.ruleset_id` pinned at start;
  - a cached `RulesetRepository` replacing `DefaultRulesetService`;
  - `GET /api/rulesets/{key}/{version}`;
  - the pin in `GetGameResponse`;
  - the client fetches the pinned ruleset and drops its embedded copy.
- Out:
  - the admin role and publishing at run time (a later plan; the `created_by` column exists and is always null for now);
  - choosing a ruleset in the lobby (open question 6).

## Affected areas
- **Domain:**
  - `Entities/RulesetVersion.cs`: `BaseEntity` plus `CreatedAt`, `Key`, `Version`, `Name`, `Hash`, `Document`, `CreatedBy?`.
  - `Game.RulesetId` (Guid?), set by `Start`.
- **Application.Common:** a `DbSet<RulesetVersion>` in `IApplicationDbContext`.
- **Application:**
  - `Rulesets/Repositories/RulesetRepository.cs`, the first repository. HybridCache holds the parsed Core `Ruleset` by row id (immutable, long expiry) and the latest version per key (short expiry, e.g. 1 minute). It checks with microsoft-docs how HybridCache keeps an immutable instance in L1 (sealed + `[ImmutableObject(true)]` on `Ruleset`).
  - `Rulesets/Contracts/GetRulesetContract.cs`, and `Rulesets/Models/RulesetErrors.cs` (`ruleset.not_found`).
  - The Games contracts use the repository:
    - Start takes the latest `default`;
    - SendCommand, Get and Watch take the game's pinned id.
  - `GetGameResponse` gains `RulesetPin(Key, Version, Hash)?` (null in a lobby).
  - `DefaultRulesetService` and the Application's embedded file are removed.
- **Infrastructure:**
  - `Persistence/Configurations/RulesetVersionConfiguration.cs`: table `rulesets`, unique `(key, version)`, max lengths (key 32, name 64, hash 64, document 1,000,000, which keeps the "every string has a max length" rule), and an FK from `games.ruleset_id` (Restrict, indexed).
  - Migration `AddRulesets`:
    - `InsertData` of default v1, with a fixed id, name "Classic", the document as a literal string and its hash;
    - an `UPDATE games SET ruleset_id = … WHERE state IS NOT NULL` for games already running.
  - HybridCache registration: `dotnet add package Microsoft.Extensions.Caching.Hybrid`.
- **Api:** `Features/Rulesets/GetRulesetEndpoint.cs`, `GET /api/rulesets/{key}/{version:int}`:
  - requires auth, like every game endpoint;
  - returns the document text as `application/json`, with `Cache-Control: private, max-age=31536000, immutable` and `ETag` set to the hash;
  - 404 `ruleset.not_found`.
- **Client:**
  - `Features/Games/Clients/RulesetsClient.cs` (`HttpClient` only here);
  - `RulesetService` fetches the game's pin once per hash, parses it, checks the hash, and caches it for the session;
  - `GamePage`/`GameTable` wait for the ruleset before drawing the board;
  - the embedded copy is removed.

## Steps
1. Domain entity and `Game.RulesetId`; configuration; migration (with the seed and the backfill). Build.
2. HybridCache, `RulesetRepository`, the Games contracts, `GetRulesetContract`. Build.
3. Api endpoint. Build.
4. Client: client, service, pages. Build.
5. Tests, docs and rules, then `/verify`, review (including `ef-core-specialist` on the migration, and `security-auditor` for the new endpoint), and the PR.

## Tests
- **Integration:**
  - `GetRulesetEndpointTests`:
    - 200 returns exactly the file's text, with the headers;
    - 404 for an unknown key or version;
    - 401 without a token;
    - 400 for a bad key.
  - `RulesetSeedTests`: every `rulesets/*/v*.json` file has a row whose `document` is identical and whose `hash` is Core's hash of it. This fails if a published file is edited.
  - `StartGameEndpointTests`: a started game is pinned to the latest default, and `GetGame` returns the pin.
  - The command, get and watch tests stay green.
  - `GovernApiFixture`: Respawner ignores `rulesets`.
  - The `GamesApi` domain seed helpers pass a ruleset.
- **Unit (Domain):** `Game.Start` stores the ruleset id.
- **Client:**
  - `RulesetsClientTests` (200, 404);
  - `RulesetService` fetches once per hash and rejects a hash mismatch;
  - `GamePage` shows loading until the ruleset arrives.
- **Architecture:** `Rulesets` feature folders (existing templates cover them); `EndpointAuthorizationTests` covers the new route.

## Docs & rules
- New `docs/features/rulesets.md`: the table, seeding, pinning, caching and the endpoint. Also `games.md` (the pin), `game-data.md` (lifecycle implemented) and `docs/README.md`.
- `.claude/rules/caching.md`: the HybridCache repository pattern (the rule asks the first repository to extend it).
- `.claude/rules/persistence.md`: seed immutable reference data with `InsertData` in the migration, never edit a published seed, and add a test pinning it.
- `docs/plans/ruleset-open-questions.md`: fold in questions 5 (authenticated, not public) and 6 (one `default` for now); keep 7–11 open.

## Verification
- `/verify` green with the full run shown.
- `ef-core-specialist` on the migration.
- Manual: a new game gets the pin, `GET /api/rulesets/default/1` returns the file, and the board plays.
- `security-auditor`, because of the new endpoint.

## Risks & open questions
- **Games already running when B deploys** get pinned to v1 by the migration's backfill. v1 equals today's constants, so they keep playing unchanged.
- **HybridCache and the `Ruleset` instance:** if marking `Ruleset` immutable isn't enough for L1 reuse, cache the document text and parse once per id in a small in-memory map inside the repository.
