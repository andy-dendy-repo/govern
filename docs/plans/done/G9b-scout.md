---
title: "Plan: G9b, the scout"
tags: [plan, game, units, recon, scout, ruleset, board, unit-models]
code: [rulesets/default/v8.json, src/Govern.Client/Features/Games/Helpers/PieceModels.cs, src/Govern.Client/wwwroot/js/board3d/pieces.js, tools/unit-models/bake.mjs]
updated: 2026-10-03
---

# Plan: G9b, the scout

**Status:** done 2026-10-03 in PR #PRNUM (backlog G9b). Asked for by the owner ("Lets add a new unit: scout"); planned and built in one PR. Decided while building: sight is 4, not 3, because the ruleset requires a unit's sight to reach as far as it can move (the fog previews stay exact).

## Context
G9 left "recon" open in its roster. The owner wants a scout: a cheap, fast unit that explores and watches the map. Every unit's numbers come from the ruleset, so the scout is new data in a new ruleset version plus its look on the board; Core gets no new rule. It stays independent of unit auto moves (a separate item built at the same time).

## Scope
- In:
  - **Scout** in ruleset `v8.json`, class `recon` (new, no counters): movement 4, sight 4 (the ruleset requires sight at least as far as a unit can move, so the fog previews stay exact), strength 8, no attack ability (it defends when attacked, so one hit doesn't destroy it, but it can't attack or capture a city), production cost 3, resource cost 1 Food, upkeep 1 Food (no Water). The fastest, farthest-seeing and cheapest unit, and the weakest that fights back.
  - **Board model:** the KayKit hooded rogue with a dagger, cape in seat colour, baked by `tools/unit-models`; a `unit-scout` icon for its banner and the panels.
  - **Rules reference and docs** list it.
- Out: special scout rules (ignoring terrain costs, extra sight on hills, upgrades), changing the starting units or a city's first production, auto explore (belongs to the auto moves item).

## Affected areas
- `rulesets/default/v8.json`: `v7.json` plus the scout, the `recon` class text and `unit.scout.name`. `v7.json` is not edited.
- `src/Govern.Application/Govern.Application.csproj`, `Games/Services/DefaultRulesetService.cs`, `src/Govern.Client/Govern.Client.csproj`, `Features/Games/Services/RulesetService.cs`, `tests/Govern.UnitTests/Core/Builders/TestRulesets.cs`: embed or read `v8.json`.
- `src/Govern.Client/Features/Games/Enums/PieceModel.cs` (`Scout`), `Helpers/PieceModels.cs` (`scout`), `Components/Game/GameSprite.razor` (`unit-scout` symbol), `wwwroot/js/board3d/pieces.js` (`scout` figure), `wwwroot/models/units/scout.json` (baked).
- `tools/unit-models/bake.mjs`: `Rogue_Hooded` download and hash, `scout` entry.

## Steps
1. Core: none (the scout uses existing fields; a defending unit without an attack ability is already allowed).
2. Ruleset: add `v8.json`, point the Api, client and unit tests at it.
3. Client: `PieceModel.Scout`, its `PieceModels` entry, the sprite symbol, the `pieces.js` figure; bake `scout.json`.

## Tests
- Unit: the default ruleset parses and validates (existing); a scout defending against a soldier takes damage and survives the first hit, and has no attack targets (`CombatForecastTests` / rules tests); an enemy 4 tiles away is seen by a scout but not by a horseman.
- Integration: none new (no server code changes; the existing game tests run on v8).
- Client: `PieceModelsTests` (every ruleset unit has a model) and `Board3DPieces_EveryFigureHasItsBakedModel` cover the new entry; the units panel shows the scout's name and icon.
- Architecture: none, existing rules cover it.

## Docs & rules
- `docs/game/rules.md` unit, cost and upkeep tables (checked by `RulesReferenceTests`), `docs/game/game-data.md` (v8 and the scout entry), `docs/game/design-roadmap.md` (G9 recon done), `docs/features/game-ui.md` (unit models), `tools/unit-models/README.md`.
- Close-out in this PR: plan to `docs/plans/done/`, backlog "Recently done".

## Verification
`dotnet build Govern.slnx` and `dotnet test Govern.slnx` green; in the running client, a city can produce a scout and the board draws it with its model.

## Risks & open questions
- The numbers above are a first pass for the owner to adjust (they are data, a later version can change them).
