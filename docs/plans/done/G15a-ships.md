---
title: "Plan: G15a, ships built in harbours"
tags: [plan, game, units, naval, ships, harbour, port, science, ruleset, combat, board]
code: [rulesets/default/v12.json, src/Govern.Core/Units/UnitDomain.cs, src/Govern.Core/Rulesets/NavalRulesValidator.cs, src/Govern.Core/Map/ReachabilityCalculator.cs, src/Govern.Core/Actions/EndTurnAction.cs, src/Govern.Core/Actions/SetProductionAction.cs, src/Govern.Core/Engine/CombatCalculator.cs, src/Govern.Client/wwwroot/js/board3d/pieces.js]
updated: 2026-10-04
---

# Plan: G15a, ships built in harbours

**Status:** done 2026-10-04 in PR #PR (backlog G15, split: this is G15a; the rest is G15b, Deferred). Asked for by the owner: "Lets add naval units that can be produced in harbours, check our plan for naval units".

## Context
The [G15 design](../../game/design-roadmap.md#g15-ships-and-naval-movement) has ships built by cities with a harbour, sailing water tiles, carrying land units and sailing rivers in state 2 and 3. The harbour and port exist (G10a, G11a: Sailing unlocks the harbour, Shipbuilding the port) but do nothing beyond yields, and the only water terrain is the lake. This first slice makes harbours build warships that sail lakes and fight, without transport or river sailing, so naval play starts small and every rule stays exact in the previews.

## Scope
- In:
  - **Water and domains:** a terrain flag `isWater` (lake: yes). A unit's new `domain` is `land` (default) or `naval`. Land units walk passable land tiles as today; naval units sail water tiles only. Mountains stay closed to both.
  - **Two ships** in ruleset `v12.json` (numbers are a first pass, data the owner can tune):
    - **Galley** (melee, naval), unlocked by Sailing (with the harbour): movement 3, sight 3, strength 20, production 6, cost 4 Wood, upkeep 1 Food.
    - **Bireme** (ranged, naval), unlocked by Shipbuilding (with the port): movement 3, sight 3, strength 12, ranged 22 at range 2, production 8, cost 4 Wood and 2 Stone, upkeep 1 Food 1 Wood.
  - **Production:** a city may produce a naval unit only when it has a harbour or port (`units[].requiresBuilding`, `RuleViolations.MissingBuilding` otherwise; like the trader's trade post, a damaged building still counts). The ship appears on the first free water tile next to the harbour's (or port's) tile; when every such tile is taken, it waits like a surrounded city does today.
  - **Combat:** melee attacks only reach a unit of the same domain (a galley rams ships, never land units; land melee can't attack ships). Ranged attacks cross domains: a bireme shoots ships and land units on the shore within range, and archers and crossbowmen shoot ships. Water gives no defence bonus; classes and counters work as today.
  - **Healing:** a ship next to its owner's harbour or port heals at the in-own-city rate; elsewhere at the normal rate.
  - **Previews, auto moves, fog:** the reachability, destination preview and combat forecast use the same domain rule, so `Execute` matches them (parity tests). Upkeep outside territory works as for land units (the 7 tiles around the ship).
  - **Client:** ship figures built in code in `pieces.js` (hull, sail in the seat colour), sprite icons, unit names, the city's production list hides ships without a harbour and says why.
- Out (G15b, back to Deferred with a trigger): carrying land units (boarding, unloading, losing them with the ship), sea and ocean terrain and a water-heavy map, river sailing (its edge graph), ships entering cities, bombarding cities, more ship types (transport, later eras).

## Affected areas
- `rulesets/default/v12.json`: `v11.json` plus `isWater`, the two ships with `domain`, their science unlocks and texts. `v11.json` is not edited.
- Core: `Map/TerrainDefinition.cs` (`IsWater`), `Map/TerrainDefinition.cs` (`AllowsDomain`), `Units/UnitDefinition.cs` + new `Units/UnitDomain.cs`, `Map/ReachabilityCalculator.cs`, `Visibility/ViewPreviewCalculator.cs`, `Actions/EndTurnAction.cs` (`SpawnTile`, healing), `Actions/SetProductionAction.cs`, `Engine/CombatCalculator.cs` / `Engine/AttackTarget.cs`, `Engine/RuleViolations.cs`, `Rulesets/NavalRulesValidator.cs` (a water terrain isn't passable; a naval unit needs a water terrain and a `requiresBuilding` that requires water next to it, and only attacks; starting units and the first production are land units needing no building), `Rulesets/RulesetValidator.cs` (fog invariant per domain).
- Application / Client: embed `v12.json` (`DefaultRulesetService`, `RulesetService`, csproj files, `TestRulesets`).
- Client: `Features/Games/Enums/PieceModel.cs`, `Helpers/PieceModels.cs`, `Components/Game/GameSprite.razor`, `wwwroot/js/board3d/pieces.js`, the production list in the city panel.

## Steps
1. Core: `UnitDomain`, `IsWater`, `AllowsDomain`; reachability and the view preview step cost by domain.
2. Core: production rule (harbour needed), spawn tile by domain, healing next to the harbour, same-domain melee in attack targets and the forecast; validator checks.
3. Ruleset: `v12.json`, point the Api, client and unit tests at it.
4. Client: ship figures and icons, production list reason.

## Tests
- Unit: ships sail lakes and can't step on land; land units still can't enter lakes; a city without a harbour can't produce a galley, with one it can, and the galley appears on water next to the harbour (waits when blocked); a galley can't attack a land unit and a soldier can't attack a galley; a bireme hits a shore unit and an archer hits a ship; healing next to own harbour; previews equal `Execute` for ships; validator rejects a naval unit with no water terrain.
- Integration: none new (no server code changes; existing game tests run on v12).
- Client: `PieceModelsTests` and `Board3DPieces_EveryFigureHasItsBakedModel` (or the built-in figure list) cover the ships; the production list shows the harbour reason.
- Architecture: none, existing rules cover it; `RulesReferenceTests` checks the new rows.

## Docs & rules
- `docs/game/rules.md` (units, costs, upkeep, a Ships section, science unlocks), `docs/game/game-data.md` (v12, `isWater`, `domain`), `docs/game/game-logic.md` (movement and combat by domain), `docs/game/design-roadmap.md` (G15 status), `docs/features/game-ui.md` (ship figures).
- Close-out in this PR: plan to `docs/plans/done/`, backlog: G15a in "Recently done", G15b stays Deferred.

## Verification
`dotnet build Govern.slnx` and `dotnet test Govern.slnx` green; in the running client on The Basin, a lake city with a harbour produces a galley that sails the lake.

## Decided while building
- The production rule is generic: a unit's `requiresBuilding` (the ships name the harbour; an upgrade such as the port counts). The trader keeps its own `trade.building` check.
- Ships keep the existing classes (melee, ranged) and a separate `domain`, so the counter table and the rules reference stay as they are.
- `isWater`, `domain` and `requiresBuilding` are optional with land defaults, so older ruleset files still parse.

## Risks & open questions
- Lakes are small (The Basin has one 6×4 lake; generated lakes are patches of about 3 tiles), so ships matter little until sea terrain and a water map (G15b).
