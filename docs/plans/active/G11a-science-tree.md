---
title: "Plan G11a: the science tree (unlocks, replacing units, unit and building upgrades)"
tags: [plan, game, science, tech-tree, unlocks, upgrades, unit-upgrade, building-upgrade, ruleset-v11]
code: [rulesets/default/v11.json, src/Govern.Core/Science, src/Govern.Core/Actions, src/Govern.Client/Features/Games/Components]
updated: 2026-10-04
---

# Plan: G11a the science tree

**Status:** draft 1, 2026-10-04, waiting for Andrii's go. Split from [G11](../../game/design-roadmap.md#g11-science-and-culture-trees): this is the science tree; the culture tree is G11b.

## Context

Science is already produced (the campus yields 2) but nothing uses it. Andrii (2026-10-04): a science item unlocks more buildings, building upgrades, units and unit upgrades; each new unit replaces the previous version of it, and old units can be upgraded. The roadmap's decided rule stays: every unlocked node costs science upkeep each turn, and when points run out the newest nodes are disabled.

## Scope

- In: a per-player science pool and income; science nodes in the ruleset (cost, upkeep, prerequisites, unlocks); the Unlock command; upkeep, disabling newest first and re-enabling oldest first; units and buildings locked until their node; unit lines where a new unit replaces the old one in production; upgrading an existing unit; upgrading a building in place through city production; first content (11 nodes, 4 new units, 5 new buildings); fog of war; the full-screen science tree view, the Upgrade actions, the top strip counter; ruleset v11, state schema 4; rules and docs.
- Out: the culture tree (G11b); improvement upgrades (farm → irrigated farm) and pillage/repair (G10b); a research queue; trading science.

## Defaults proposed (need Andrii's go)

1. **Science pool per player.** Science yields no longer go into a city's stock; every turn they add to the player's pool. Each city yields 1 science on its own (like its 1 culture), so research starts before a campus.
2. **Unlock is instant:** a node can be unlocked when its prerequisites are owned and enabled and the pool holds its cost; the cost is spent at once. No research queue.
3. **Upkeep:** at the end of the player's turn, income minus the upkeep of enabled nodes goes into the pool. If the pool would drop below 0, the newest enabled node is disabled (repeat until it fits), and the pool stays at 0. A disabled node costs nothing and is re-enabled automatically, oldest first, when the surplus covers its upkeep, without paying its cost again.
4. **Disabled means:** its units and buildings can't be started or upgraded into; units and buildings that exist keep working. Nodes that need it can't be unlocked.
5. **Replacing units:** a unit can name the unit it replaces (`replaces`). Once the new one is unlocked and enabled, the old one disappears from production lists (it comes back if the node is disabled). A city already producing the old unit finishes it.
6. **Upgrading a unit:** the Upgrade action, on a unit standing in its owner's territory with movement left, turns it into the newest enabled unit of its line. It costs the new unit's `upgradeCost` resources from the city owning that tile, ends the unit's moves, keeps its health, position and orders cleared.
7. **Upgrading a building:** a building can name the building it upgrades (`upgrades`). The city produces the upgrade like any building (its production and resource cost), on the old building's tile; the old one works until the upgrade completes and then is replaced (health back to full). A city can't build the old kind once it has the new one. A trade post's trading also works for its upgrade.
8. **What stays free at the start:** settler, soldier, spearman, builder, scout; army camp, campus, culture centre. Archer, horseman, trader with trade post, and harbour move behind tier-1 nodes.

## First content (numbers are a starting point to tune)

| Node | Needs | Cost | Upkeep | Unlocks |
|---|---|---|---|---|
| Archery | — | 8 | 1 | Archer |
| Horseback riding | — | 8 | 1 | Horseman |
| Trade | — | 8 | 1 | Trade post, Trader |
| Sailing | — | 8 | 1 | Harbour |
| Bronze working | — | 12 | 1 | Swordsman (replaces Soldier) |
| Iron working | Bronze working | 20 | 2 | Pikeman (replaces Spearman), Barracks (upgrades Army camp, production 2) |
| Machinery | Archery | 20 | 2 | Crossbowman (replaces Archer), Workshop (new: production 1) |
| Stirrups | Horseback riding | 20 | 2 | Knight (replaces Horseman) |
| Education | — | 16 | 1 | University (upgrades Campus, science 4) |
| Currency | Trade | 16 | 1 | Market (upgrades Trade post, gold 4) |
| Shipbuilding | Sailing | 16 | 1 | Port (upgrades Harbour, food 4) |

New units are the same class as the one they replace, a bit stronger and dearer (Swordsman 26, Pikeman 24, Crossbowman ranged 28, Knight 28 movement 3). KayKit models are reused with a tint or a variant where one exists.

## Affected areas

- Ruleset: `rulesets/default/v11.json`: `science` section (`nodes[]`: id, cost, upkeep, requires, unlocks units/buildings; `cityYield`), `units[].replaces`, `units[].upgradeCost`, `buildings[].upgrades`, texts.
- Core: `Rulesets/ScienceRules.cs` + validator (ids exist, no cycles, every node reachable, a replaced unit and upgraded building exist, same class); `Science/` (`PlayerScience` state, `ScienceCalculator` for availability and the newest unit of a line); `Actions/UnlockNodeAction.cs`, `UpgradeUnitAction.cs`; `Engine/ScienceRule.cs` (income, upkeep, disable and re-enable at end of turn); guards in `SetProductionAction`, `ProduceBuildingAction`; `TradeRule`/`SetProductionAction` read "has a trade post or its upgrade"; `YieldCalculator` sends science to the pool; events `NodeUnlockedEvent`, `NodeStateChangedEvent`, `UnitUpgradedEvent`; `Visibility/` private `ScienceView` in `PlayerView`; `GameState` schema 4 reading 3.
- Application: the default ruleset service loads v11.
- Client: `Components/Screen/ScienceTree.razor` (full-screen view, the G1 `TreeView` from the catalog), the science counter in the top strip opening it, Upgrade in the unit card, upgrade entries in the city's Buildings tab, log lines.

## Tests

- Unit (Core): validator cases; unlock (cost, prerequisites, disabled prerequisite); income and upkeep, disabling newest first, re-enabling oldest first; production lists with replacement; unit upgrade (territory, cost, moves, health); building upgrade (tile, replaced on completion, trade post upgrade still trades); visibility (tree private); serializer reads schema 3; parity of the client previews; `RulesReferenceTests` tables.
- Integration: the game endpoints accept the new commands (happy path, rule violation 400/409 as today's commands do).
- Client (bUnit): science tree view states, the counter, the Upgrade action, the building upgrade entry.
- Architecture: none, existing rules cover it.

## Docs & rules

- `docs/game/rules.md` (Science section, unit and building tables), `docs/game/game-data.md` / `game-data-economy.md` (science format), `docs/game/game-logic.md`, `docs/features/game-ui.md` (TreeView now built), `docs/game/design-roadmap.md` (G11 split), backlog close-out.

## Verification

`dotnet build Govern.slnx`, `dotnet test Govern.slnx` green; Playwright on the running client: unlock a node, see the soldier replaced by the swordsman, upgrade a soldier, upgrade a campus; screenshots light and dark, 1400 and 390.

## Risks & open questions

- Upkeep may bite hard early; the numbers above give 2–3 science a turn per city with a campus, so 4–6 nodes at once before universities. Tune in play.
