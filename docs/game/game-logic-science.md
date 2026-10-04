---
title: Game logic, science (the science budget, unlocking, locks, unit lines and upgrades)
tags: [game, core, rules, algorithms, science, science-tree, nodes, budget, unlock, disable, locked, replaces, unit-upgrade, building-upgrade, previews, fog-of-war]
code: [src/Govern.Core/Engine/ScienceRule.cs, src/Govern.Core/Science, src/Govern.Core/Actions/UnlockNodeAction.cs, src/Govern.Core/Actions/UpgradeUnitAction.cs, src/Govern.Core/Actions/UpgradeBuildingAction.cs, src/Govern.Core/Visibility/SciencePreviewCalculator.cs]
updated: 2026-10-04
---

# Game logic: science

*G11a, decided 2026-10-04 (the owner's model; not a Civ copy).* How `Govern.Core` runs the science tree, step by step, in the style of [game logic](game-logic.md): every number is a ruleset key in brackets ([game data: science](game-data-science.md)). The player's version is [Science in the rules reference](rules.md#science).

## Science terms

| Term | Meaning |
|---|---|
| Science per turn (income) | The amount of [`science.resource`] in the income of every city the player owns now: [`cities.yields`] (1 a city) plus working buildings' [`buildings.*.yields`] (campus 2, university 4) plus tile yields. Never stored: each city's stock skips it |
| Node | An entry of [`science.nodes`]: its points, prerequisites and the units and buildings it unlocks |
| Owned node | A node the player unlocked, in unlock order; never dropped. Either enabled or switched off |
| Used | The sum of [`science.nodes.*.points`] over the player's enabled nodes |
| Locked | A unit or building listed by a node is locked unless the player owns that node and it is enabled; anything no node lists is never locked |
| Newest of a line | From a unit, follow [`units.*.replaces`] backwards (the unit replacing it, then the one replacing that, …) and take the last one that is not locked; the unit itself when none is |

## Unlock a node

1. The node must exist → `science.unknown_node`.
2. The player must not own it already (enabled or not) → `science.node_owned`.
3. Every node in its [`requires`] must be owned and enabled → `science.needs_prerequisite`.
4. Its [`points`] must fit in income − used → `science.not_enough`.
5. The node is added at the end of the player's nodes, enabled (`science_node_changed`, unlocked). Nothing is paid: the node takes its points every turn from now on.

## Balancing the budget (after every command)

After every command (any player's, so a capture or a damaged campus is noticed at once), for every player who owns a node, with their income now:
1. While used > income, switch off the newest enabled node (`science_node_changed`, disabled).
2. Then go through the switched-off nodes, oldest first: switch one on again (`science_node_changed`, enabled) when its points fit in income − used and every node it requires is enabled; stop at the first that doesn't fit, so an older node is never skipped for a newer one.

A switched-off node takes nothing, locks its units and buildings again, and blocks unlocking nodes that require it. Units and buildings that already exist keep working.

## Production under locks

- **Set production:** after the unit exists (`city.unknown_unit_type`), it must not be locked → `science.locked`, and must be the newest of its line → `science.replaced`. Choosing a trader also needs a city with [`trade.building`] or an upgrade of it.
- **Produce a building:** after the building exists, it must not be an upgrade [`buildings.*.upgrades`] → `city.building_is_upgrade`; the city must not have it or any building it upgrades into → `city.building_exists`; it must not be locked → `science.locked`; then the site checks of [Produce a building](game-logic.md#produce-a-building).
- **What a city goes on with:** after it finishes a unit, a city produces the newest of that unit's line; if that is locked (its node was switched off), the newest of the line of [`cities.initialProduction`]. A new city starts with the newest of that line; a captured city switches to the newest its new owner may produce.
- A city keeps finishing an item it started before the item became locked or replaced.

## Upgrade a unit

1. The unit must be the actor's → `unit.not_found`.
2. It must not be a trader on a route → `trade.on_route`.
3. The newest of its line must be another unit → `upgrade.none`.
4. It must have movement left → `unit.no_movement`.
5. Its tile must be in the territory of one of the actor's cities → `upgrade.not_in_territory`.
6. That city's stock must hold the new unit's [`units.*.upgradeCost`] → `upgrade.missing_resources`.
7. The city pays the cost; the unit becomes the new type with its damage, position and id kept, 0 movement left, and no job or destination (`unit_upgraded`).

## Upgrade a building (a builder's job)

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have `build_improvement` → `unit.cannot_build_improvements`.
3. Its tile's building must have an upgrade [`buildings.*.upgrades`] → `upgrade.none`.
4. The tile must be in the territory of one of the actor's cities → `upgrade.not_in_territory`.
5. The upgrade must not be locked → `science.locked`.
6. It must have movement left → `unit.no_movement`; and not be upgrading this building already → `build.already_building`.
7. The unit's job becomes the upgrade with progress 0, and its movement drops to 0 (`build_started`).

At the end of the owner's turn the job advances like any other ([Building with units](game-logic.md#building-with-units-jobs)): the builder's [`buildProduction`] plus its city's production, up to the upgrade's [`buildings.*.cost`]. Each turn the job is checked again (the building is still there and in the actor's territory, the upgrade still unlocked) and dropped when it fails. When the progress reaches the cost, the city owning the tile pays the upgrade's [`buildings.*.resourceCost`]; while it can't, the finished job waits and takes no more production. Once paid, the new building replaces the old one in the city's buildings and on the tile, at full health, and the job ends (`building_upgraded`).

A rule that asks for a building in any version (the trade post's market and traders, "the city has this building already") counts an upgrade of it, following [`upgrades`] links.

## What each player sees

- A player's income and nodes are in their own view only; an opponent's science is never sent.
- `science_node_changed` is in its player's log only. `unit_upgraded` and `building_upgraded` are seen by whoever sees the tile; an opponent's `building_upgraded` carries no city.

## Science previews

From the view alone, Core answers with the rules' own checks (so the answer equals the engine's): whether a node can be unlocked (the view holds the player's income and nodes), whether a unit can be upgraded (its tile's territory city and that city's stock are the player's own), and whether a builder can start a building upgrade (the player's territory is always visible, with its buildings).
