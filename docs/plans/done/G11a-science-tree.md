---
title: "Plan G11a: the science tree (science budget, unlocks, replacing units, unit and building upgrades)"
tags: [plan, game, science, tech-tree, unlocks, budget, upgrades, unit-upgrade, building-upgrade, ruleset-v11]
code: [rulesets/default/v11.json, src/Govern.Core/Science, src/Govern.Core/Engine/ScienceRule.cs, src/Govern.Core/Actions, src/Govern.Client/Features/Games/Components/Screen/ScienceTree.razor, src/Govern.Client/Components/Game/TreeView.razor]
updated: 2026-10-04
---

# Plan: G11a the science tree

**Status:** done 2026-10-04 (PR #TBD). Split from [G11](../../game/design-roadmap.md#g11-science-and-culture-trees): this is the science tree; the culture tree is G11b, which goes next.

## Context

Science was produced (the campus yields 2) but nothing used it. Andrii (2026-10-04): a science item unlocks more buildings, building upgrades, units and unit upgrades; each new unit replaces the previous version of it, and old units can be upgraded. His answers fixed the model: science is **not accumulated**, a player has a certain amount of science per turn and spends it on items of the tree; every city makes 1 science on its own; items keep taking their science (prerequisites too); a player can't drop an item; upgrading a building needs a builder.

## Scope

- In: science per turn (cities' income, never stored); science nodes in the ruleset (points, prerequisites, units and buildings they unlock); Unlock; balancing after every command (switch off newest, switch on oldest); locked units and buildings; unit lines (`replaces`) where cities produce only the newest; upgrading a unit in own territory (`upgradeCost`); upgrading a building with a builder's job (`upgrades`); first content (11 nodes, 4 units, 5 buildings); fog of war; the science counter and the science tree view (`TreeView`), the Upgrade actions, log lines; ruleset v11; rules and docs.
- Out: the culture tree (G11b); improvement upgrades, pillage and repair (G10b); a research queue; trading science.

## Decisions (Andrii's model)

1. **Science per turn, no pool.** Income = the science resource in all the player's cities' income now (`cities.yields` gives every city 1; campus 2, university 4). City stocks skip it.
2. **Unlock costs nothing at once:** a node can be unlocked when its points fit in income − used and its prerequisites are owned and enabled; it then takes its points every turn. No research queue, no dropping.
3. **Balance after every command**, for every player: while used > income, switch off the newest enabled node; then switch on switched-off nodes oldest first while they fit and their prerequisites are on, stopping at the first that doesn't fit.
4. **Switched off means:** its units and buildings can't be started or upgraded into; existing ones keep working; nodes that need it can't be unlocked.
5. **Replacing units:** `replaces` names the older unit of the same class. Cities produce only the newest unlocked version; a city already producing an older one finishes it, then goes on with the newest.
6. **Upgrading a unit:** in own territory with movement left; the tile's city pays the new unit's `upgradeCost`; health, position and id kept; moves end; job and destination cleared.
7. **Upgrading a building:** `upgrades` names the older building. A city never produces an upgrade; a builder on the building starts a job (builder's production plus its city's, up to the upgrade's `cost`). The city pays the upgrade's `resourceCost` when the job is done (the job waits while it can't), then the new building replaces the old one at full health. A city counts as having a building when it has it or an upgrade of it (a market trades like a trade post).
8. **Free at the start:** settler, soldier, spearman, builder, scout; army camp, campus, culture centre. Archer, horseman, trader with trade post, and harbour moved behind 1-point nodes.

## Content (ruleset v11)

| Node | Points | Requires | Unlocks |
|---|---|---|---|
| Archery | 1 | — | Archer |
| Horseback riding | 1 | — | Horseman |
| Trade | 1 | — | Trader, Trade post |
| Sailing | 1 | — | Harbour |
| Bronze working | 1 | — | Swordsman (replaces Soldier) |
| Education | 2 | — | University (upgrades Campus, science 4) |
| Iron working | 2 | Bronze working | Pikeman (replaces Spearman), Barracks (upgrades Army camp, production 2) |
| Machinery | 2 | Archery | Crossbowman (replaces Archer), Workshop (new, production 1) |
| Stirrups | 2 | Horseback riding | Knight (replaces Horseman) |
| Currency | 2 | Trade | Market (upgrades Trade post, gold 4) |
| Shipbuilding | 2 | Sailing | Port (upgrades Harbour, food 4) |

New units are a bit stronger and dearer than the unit they replace (Swordsman 26, Pikeman 24, Crossbowman ranged 28, Knight 28 with movement 3); their KayKit models reuse the older unit's.

## Affected areas

- Ruleset: `rulesets/default/v11.json` (`science` section, `units[].replaces`/`upgradeCost`, `buildings[].upgrades`, texts); the Api, client and tests load v11.
- Core: `Science/` (`ScienceNode`, `OwnedNode`, `ScienceNodeChange`, `ScienceCalculator`), `Rulesets/ScienceRules.cs`, `ScienceRulesValidator.cs`; `Player.Nodes`, `UnitJob.UpgradeTo`; `Actions/UnlockNodeAction.cs`, `UpgradeUnitAction.cs`, `UpgradeBuildingAction.cs`; `Engine/ScienceRule.cs` (dispatch, income, balance), `Engine/CommandDispatcher.cs` (moved out of `GameEngine`); guards in `SetProductionAction`, `ProduceBuildingAction`, `ConstructionRule`; events `ScienceNodeChangedEvent`, `UnitUpgradedEvent`, `BuildingUpgradedEvent`; `Visibility/ScienceView`, `SciencePreviewCalculator`. No state schema bump: `Player.Nodes` is optional.
- Client: `Components/Game/TreeView.razor`, `Screen/ScienceTree`, the science counter in `TopBar`, `GameUiService.ScienceOpen`, Upgrade buttons in `ActionBar`, filtered lists in `CityPanel`, log lines, sprites and models for the new units and buildings.

## Tests

- Unit (Core): validator cases; unlock, budget balancing (newest off, oldest on, prerequisites), locks, unit lines and next production, unit upgrade, building upgrade (job, waiting to pay, replacing), visibility; existing tests moved to the locked content.
- Integration: the science commands over the commands endpoint (happy path, 409 codes, 400 bodies, science private).
- Client (bUnit): counter and tree, unlock, switched-off node, production lists, unit and building upgrades, job text, log lines, `TreeView`.
- Architecture: the `Science` Core folder in `Templates.CoreFolders`; `RulesReferenceTests` tables.

## Docs & rules

`docs/game/rules.md` (Science section and tables), [game data: science](../../game/game-data-science.md), [game logic: science](../../game/game-logic-science.md), `game-data.md`, `game-data-economy.md`, `game-logic.md`, `docs/features/game-ui.md`, `docs/features/game-core.md`, `docs/game/design-roadmap.md` (G11 split), `docs/architecture.md`, `.claude/rules/game-core.md` and `architecture.md` (Science folder, `CommandDispatcher`), backlog.

## Verification

`dotnet build Govern.slnx` and `dotnet test Govern.slnx` green.

## Risks & open questions

- Balance: with 1 science a city, an early player owns one or two 1-point nodes per city before a campus. Tune in play.
