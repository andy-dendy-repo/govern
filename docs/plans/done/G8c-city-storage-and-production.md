---
title: "Plan: G8c city storage caps, production breakdown, no unit base disc"
tags: [plan, game, city, economy, storage, caps, production, ruleset, ui, board3d]
code: [src/Govern.Core/Cities, src/Govern.Core/Engine, src/Govern.Core/Rulesets, rulesets/default, src/Govern.Client/Features/Games/Components/CityPanel.razor, src/Govern.Client/wwwroot/js/board3d/pieces.js]
updated: 2026-10-05
---

# Plan: G8c city storage caps, production breakdown, no unit base disc

**Status:** done 2026-10-05 (backlog G8c, PR #65). Asked by the owner on 2026-10-05 together with population, which the owner put out of scope (it stays in G8b).

## Context
A city's stock grows without limit: every turn its territory and buildings add their yields (`ConstructionRule.CollectYields`), so a quiet city hoards forever. The owner wants a storage cap per resource: a default for every city plus what each building adds. The city panel also only shows a production progress bar, not where the production comes from or what it goes to, and the 3D board draws a seat-coloured disc under every unit, which the owner wants gone.

## Scope
- In: per-resource storage caps (ruleset v14: a default per stocked resource, building additions), stock cut to the caps at the end of every turn; the city panel shows `amount / cap`, the production a turn with its sources (city base, each working building) and what it is spent on (its item, a builder's job it feeds, or nothing while the resource cost waits); no base disc under units.
- Out: population, housing, loyalty and the production queue (G8b, deferred). Science is a per-turn budget, not stocked, so it has no cap.

## Affected areas
- Core: `Rulesets/CityRules.cs` (`Storage`), `Economy/BuildingDefinition.cs` (`Storage`), `Rulesets/EconomyRulesValidator.cs`, `Cities/CityStorage.cs` (new calculator: caps of a city, cut a stock), `Actions/EndTurnAction.cs` (cut every city at the end of the turn), `Cities/CityProduction.cs` (breakdown), `Visibility/CityForecast` + `UpkeepPreviewCalculator.cs` (caps and what overflows).
- Ruleset: `rulesets/default/v14.json` (copy of v13 plus storage); embedded names in `Govern.Application.csproj`, `Govern.Client.csproj`, `DefaultRulesetService`, `RulesetService`.
- Client: `Features/Games/Components/CityPanel.razor(.cs)`, `wwwroot/js/board3d/pieces.js`.

## Steps
1. Core: `CityRules.Storage` (list of resource caps, required for every resource but the science one), `BuildingDefinition.Storage` (may be empty); validator checks ranges, starting stock within the caps.
2. Core: `CityStorage.Caps(ruleset, workingBuildings)` = default + each working building's storage (an upgrade replaces its building, so it brings its own full amount); `CityStorage.Cut(stock, caps)`. `EndTurnRule` cuts every city's stock (both players: trade delivers to the other side) as its last economy step, so this turn's income can still pay upkeep and costs before the excess is lost.
3. Core: `CityProduction.Breakdown` (base + per building) and the forecast reports caps and the amount lost to them.
4. Ruleset v14 defaults: food 30, water 30, wood 20, stone 20, gold 20, culture 40. Buildings: army camp +10 food, +10 water; barracks +20 food, +20 water; campus +10 gold; university +20 gold; trade post +10 food, wood, stone and gold; market +20 of each; culture centre +40 culture; harbour +10 food, +10 water; port +20 food, +20 water; workshop +20 wood, +20 stone.
5. Client: stock row `12/30 (+3)`, marked when full or overflowing; under it "Production 3 a turn: city 1, Workshop 1, Barracks 1" and "Spent on Soldier (2 of 4)", "Spent on a Builder's Farm", or "Waiting for 2 Wood"; Buildings tab shows each building's storage in its effect.
6. Client: drop the base disc from standing figures and the horseman (cities keep their ground plate).

## Tests
- Unit: caps sum default and buildings, a ruined building adds nothing, an upgrade counts once; end of turn cuts both players' cities after upkeep and production; validator rejects a missing cap and a starting stock over its cap; forecast shows the overflow; production breakdown; `RulesReferenceTests` tables.
- Client (bUnit): stock shows amount/cap and the full mark; production breakdown and each "spent on" case.
- Integration: none new (no server code changes beyond the embedded ruleset); existing game tests run on v14.
- Architecture: none, existing rules cover it.

## Docs & rules
- `docs/game/rules.md` (storage section and tables), `docs/game/game-data.md` (new fields), `docs/game/game-logic.md` (end of turn order), `docs/game/design-roadmap.md` (G8), backlog close-out.

## Verification
`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green; a Playwright look at the city panel and a unit on the board.

## Risks & open questions
- Culture is stocked and pays for territory: the 19th tile costs 40, so without a culture centre a city stops growing at 26 tiles of 37. Default: accept it (the culture centre lifts it to 80, enough for every tile).
- Running games switch to v14 at once (one embedded ruleset); a city over its cap loses the excess at its next end of turn.
