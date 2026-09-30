---
title: Ruleset B plan (store, pin, serve) (deferred)
tags: [plan, deferred, game, ruleset, database, migration, caching, pinning, versioning, lobby]
code: [rulesets, src/Govern.Core/Rulesets, src/Govern.Application/Games, src/Govern.Client/Features/Games]
updated: 2026-09-30
---

# Plan: The loadable ruleset, part B (deferred)

**Status: deferred (2026-09-30).** Storing rulesets in the database, versioning and pinning them per game, and choosing a setup in the lobby add a table, a repository, caching and an endpoint with no player-visible gain while there is one ruleset and no production games. The Api and the client keep embedding `rulesets/default/v1.json` (part A, [done](../done/ruleset-a-core.md)); new content goes into that file.

**Revisit when:** a balance change would alter games already running in production, or run-time (admin) publishing or a ruleset choice in the lobby is wanted. Tracked in the [backlog](../backlog.md) as G3b.

This plan is kept as written; recheck it against the code before picking it up, since the ruleset will have grown by then.

## Context
Every game number in `Govern.Core` is a constant or a C# switch today (`UnitCatalog`, `TerrainCatalog`, the `City` constants, `GameSetup`/`GameMap` sizes, the spawn logic in `GameFactory`). We agreed that Core keeps the mechanics and reads every customisable number, list and text from a **ruleset**:
- a JSON document in the repository (`rulesets/default/v1.json`), stored as an immutable version in a `rulesets` table;
- Core parses, validates and hashes it;
- each game is pinned to one version.

The agreed format and database record are in `docs/game/game-data.md`. This is roadmap item G3 (data-driven catalogs). The work is split into two PRs, each a working increment:
- **Ruleset A — Core reads a ruleset.** The engine takes a `Ruleset` everywhere. The Api and the client load the default from the repository file. No database change, and no change in behaviour: the file holds today's values.
- **Ruleset B — store and pin.** The `rulesets` table and its seeding migration. Games are pinned at start through a cached repository. A `GET` endpoint serves a version, and the client fetches the ruleset of the game it shows.

Both branch from `origin/stage` and PR to `stage` (`gamecore` is retired since 2026-09-30), through `/develop-feature`.

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
  - choosing a ruleset in the lobby ([question 6](#decisions-and-questions-for-part-b)).

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
- The [decisions and questions](#decisions-and-questions-for-part-b) below: record 5 (authenticated, not public) and 6 (one `default` for now) as decided.

## Verification
- `/verify` green with the full run shown.
- `ef-core-specialist` on the migration.
- Manual: a new game gets the pin, `GET /api/rulesets/default/1` returns the file, and the board plays.
- `security-auditor`, because of the new endpoint.

## Risks & open questions
- **Games already running when B deploys** get pinned to v1 by the migration's backfill. v1 equals today's constants, so they keep playing unchanged.
- **HybridCache and the `Ruleset` instance:** if marking `Ruleset` immutable isn't enough for L1 reuse, cache the document text and parse once per id in a small in-memory map inside the repository.

## Decisions and questions for part B
Moved here from the former ruleset open questions (2026-09-30).
1. **Who reads the database.** Decided: the Api loads the ruleset JSON from the database and passes the text to Core, which parses and validates it (`Ruleset.Parse(json)`). Core stays base class library only.
2. **Where the ruleset is authored.** Decided: JSON files in the repository, reviewed in PRs, then written by a migration into a `rulesets` table as immutable versions (the columns are in [game data](../../game/game-data.md#the-database-record)). Editing in the database (an admin page) may come later.
3. **Who can publish a ruleset.** Decided: both. Developers publish through a PR plus a migration, and an admin role publishes at run time. There is no admin role yet, so run-time publishing needs one (a later plan). Either way, a published version is immutable and must pass Core's validation.
4. **Pinning.** Decided: each game records its ruleset `key`, `version` and `hash`, so a balance change never alters a running game, and a replay uses the same rules.
5. **The client needs it too:** for the view previews, names, texts and icons. Proposal: an authenticated, cacheable `GET /api/rulesets/{key}/{version}` returning the same JSON.
6. **One ruleset or several:** one "default" ruleset, or several (for example "quick game" and "classic") chosen in the lobby? Selectable setups in the lobby are part of this deferral.
