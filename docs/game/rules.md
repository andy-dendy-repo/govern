---
title: Game rules reference (for players)
tags: [game, rules, reference, players, how-to-play, map, terrain, forest, hills, mountains, lake, rivers, deep-river, crossing, map-generation, units, builders, improvements, farm, mine, lumber-camp, bridges, buildings, campus, harbour, resources, stockpile, work-area, movement, attack, combat, health, damage, counters, ranged, healing, defence, cities, production, fog-of-war, victory, resign]
code: [rulesets/default/v5.json, src/Govern.Core, tests/Govern.ArchitectureTests/RulesReferenceTests.cs]
updated: 2026-10-01
---

# Game rules reference

How Govern is played today, written for players. Every number on this page comes from the game's ruleset ([`rulesets/default/v5.json`](../../rulesets/default/v5.json)); the step-by-step algorithms behind each rule, with the ruleset keys, are in [game logic](game-logic.md).

## Goal and victory

Govern is a turn-based strategy game for the number of players under "Game numbers" (two today) on a hex map. The last player standing wins.
- A player who has no city and no unit that can found a city is **eliminated**, and their remaining units disappear.
- A player can **resign** at any time, even during the opponent's turn. Their units disappear and the opponent wins.
- There are no other victory conditions yet.

## The map

- The map is a rectangle of hexagonal tiles. Each tile has 6 neighbours, and distances are counted in hex steps.
- A new game's map size is in the table under "Game numbers". Each new map is different: mostly plains, with patches of forest, hills, mountains and lakes (see "Terrain types").
- **Both halves of the map are the same.** The map turned half a turn around its centre looks exactly like itself, so neither player gets the better side. Once you have explored your own half you know the terrain of your opponent's half too. Starts are not mirrored, though: the map only hints that your opponent started on plains in their zone.
- Rivers run along the edges between tiles (see "Rivers").
- Mountains, lakes and deep rivers never cut the map in two: every tile you can walk on can be reached from every other.
- Each player starts near their own edge of the map (the first player on the left, the second on the right) with the starting units listed under "Game numbers", placed side by side on plains.

## Terrain types

A tile's terrain decides how many movement points it costs to enter, whether land units can enter it at all, whether a unit or city standing on it sees further, and how much it helps a unit defending on it.

| Terrain | Movement cost | Passable | Sight bonus | Defence bonus |
|---|---|---|---|---|
| Plains | 1 | Yes | — | — |
| Forest | 2 | Yes | — | +3 |
| Hills | 2 | Yes | +1 | +3 |
| Mountains | — | No | — | — |
| Lake | — | No | — | — |

- **Sight bonus:** a unit or city standing on the tile sees this many tiles further than its own sight.
- **Defence bonus:** a unit attacked on the tile fights with this much extra strength (see "Combat"). Attacking from it gives nothing.
- The board's **Movement cost** lens (the toolbar at the top left of the map) writes each known tile's cost on it, and ✕ where land units can't go.

## Rivers

Rivers run along the edges between tiles, not across tiles. Each one starts next to hills or mountains and flows to a lake or the edge of the map, getting deeper as it goes: a stream first, then a river, then a deep river. Like the terrain, rivers are the same on both halves of the map.

| River | Crossing | Defence bonus |
|---|---|---|
| Stream | Free | — |
| River | +1 | +3 |
| Deep river | Can't cross | +5 |

- **Crossing:** the movement points a step over the river costs on top of the tile it enters. A step over a river onto plains costs 2. Land units can't cross a deep river at all; they walk round it, or build a bridge (see "Bridges").
- **Defence bonus:** a unit attacked in melee from across the river fights with this much extra strength, on top of its terrain's defence bonus. Ranged attacks ignore rivers.
- A melee unit can attack an enemy unit across a deep river (it stays where it is), but it can't capture a city across one.
- The **Movement cost** lens marks each river edge that costs extra (+1), and ✕ on each one land units can't cross.

## Turns and rounds

- Players take turns in seat order. Only the player whose turn it is can act, except for resigning.
- A turn ends when the player ends it. There is no time limit.
- When play wraps around to the first player, the round number goes up by 1. The game starts in round 1.
- At the start of your turn, all your units get their full movement back.

## Unit types

| Unit | Class | Strength | Ranged strength | Movement | Sight | Production cost | Build production | Abilities |
|---|---|---|---|---|---|---|---|---|
| Settler | Civilian | — | — | 2 | 2 | 6 | — | Found city |
| Soldier | Melee | 20 | — | 2 | 2 | 4 | 1 | Attack, Build bridges |
| Spearman | Anti-cavalry | 18 | — | 2 | 2 | 4 | — | Attack |
| Horseman | Cavalry | 20 | — | 3 | 3 | 6 | — | Attack |
| Archer | Ranged | 10 | 22 (range 2) | 2 | 2 | 5 | — | Ranged attack |
| Builder | Civilian | — | — | 2 | 2 | 5 | 2 | Build improvements, Build bridges |

- **Class:** what the unit is strong or weak against (see "Class counters").
- **Strength:** how hard the unit fights in melee and when it is attacked. A unit with no strength (a civilian) can't fight: any attack destroys it.
- **Ranged strength:** how hard the unit shoots, and the farthest tile it can shoot at.
- **Movement:** movement points per turn. Entering a tile costs its terrain's movement cost, plus the crossing cost of a river on the way (see "Rivers").
- **Sight:** how many tiles away the unit can see, plus the sight bonus of the terrain it stands on.
- **Production cost:** production points a city needs to build the unit.
- **Build production:** production points the unit adds each turn to what it is building (see "Building with units").
- **Found city:** the unit can turn into a city (see "Founding a city").
- **Attack:** the unit can attack an enemy unit or city on a neighbouring tile (see "Attacking").
- **Ranged attack:** the unit can shoot an enemy unit within its range (see "Attacking").
- **Build improvements:** the unit can build an improvement on its tile in your work area (see "Improvements").
- **Build bridges:** the unit can bridge the river between its tile and a neighbouring one (see "Bridges").

## Moving a unit

- A unit moves along the cheapest path to a tile it can reach with the movement it has left. The movement spent is the path's total cost.
- A step can't cost more than the movement left: a unit with 1 movement left can't enter forest or hills, or cross a river, that turn.
- A unit can't enter a tile with another unit on it, an enemy city, or impassable terrain, and it can't cross a deep river. It can enter your own cities.
- Only one unit can stand on a tile.
- A tile blocked by an enemy unit you can't see is simply unreachable; the game doesn't tell you why, so it reveals nothing about the hidden unit.

## Attacking

- A unit with Attack or Ranged attack that still has movement left can attack. Attacking uses up all its movement for the turn, and it stays where it is.
- **Attack (melee):** an enemy unit or city on a neighbouring tile. Both units fight and both take damage (see "Combat").
- **Ranged attack:** an enemy unit up to the unit's range away. Only the target takes damage: it can't hit back. A ranged unit can't attack a city with no unit on it.
- **Attacking a city with no unit on it** (melee only, and not across a deep river) captures the city. The attacker moves into it with 0 movement, the city becomes yours, and its production progress resets when "Capturing a city resets its production" is Yes under "Game numbers".
- A city with a unit on it is defended: fight the unit first.
- Before you attack a unit, the game shows the forecast: both units' strength in this fight, what changes it, and how much damage each side can take. Click the forecast's Attack button to attack, or Cancel (Esc). The damage always lands within the forecast.

## Combat

- Every unit has full health (under "Game numbers") when built. A unit that drops to 0 health is destroyed. When both units drop to 0, both are destroyed.
- **Strength in a fight** starts from the unit's strength (its ranged strength when it shoots) and changes with:
  - **Health:** a hurt unit fights weaker, down to half its strength near 0 health (strength × (full health + health) ÷ (2 × full health), rounded down).
  - **Class counters:** the bonus in the table below, whether the unit attacks or defends.
  - **Terrain:** the defender gets the defence bonus of its tile.
  - **River:** against a melee attack across a river, the defender also gets the river's defence bonus (see "Rivers").
- **Damage** each side takes: the damage between equal units, plus the extra damage per point by which the other side is stronger (less when it is weaker), kept between the least and the most damage under "Game numbers". A random roll then moves it up or down by up to the random spread, but never below 1, and never more than the health left.
- **Civilians** (the settler and the builder) are destroyed by any attack and do no damage back.
- **Healing:** at the end of your turn, each of your hurt units that neither moved, attacked nor worked on a job this turn heals, more in one of your cities (under "Game numbers"), up to full health.

## Class counters

| Class | Strong against | Bonus |
|---|---|---|
| Melee | Anti-cavalry | +5 |
| Anti-cavalry | Cavalry | +10 |
| Cavalry | Ranged | +5 |

- Ranged units counter melee in their own way: they shoot from two tiles away and take no damage back.

## Founding a city

- A unit with the Found city ability that still has movement left can found a city on its own tile. The unit is used up.
- A city can't be founded on another city, or closer to any city (yours or the enemy's, seen or not) than the minimum distance under "Game numbers".
- A new city is named "City n", where n is the number of cities you own, including the new one. It starts producing the unit in "First unit a new city produces" under "Game numbers".

## Cities and production

- Each city produces one unit type at a time. You can change it at any time during your turn; progress is kept.
- When you end your turn, each of your cities adds its production per turn (under "Game numbers") to its progress. When the progress reaches the unit's production cost, the unit appears on the city's tile, or on the first free neighbouring tile a unit can stand on if the city's tile is taken, with 0 movement. Progress then starts again from 0.
- If every tile around the city is taken, the finished unit waits and appears in a later turn.
- Cities see as far as the city sight under "Game numbers", plus the sight bonus of their tile.

## Building with units

Builders and soldiers build things with their own **build production** (under "Unit types"). There are no charges: a unit can build as often as you like.
- To start, select the unit and pick what to build in the action bar. Starting uses up the unit's movement for the turn, and the unit needs movement left to start.
- At the end of each of your turns, the job gains the unit's build production. **On your city's ground** (the city's tile or its work area), the city's production is added too, and that city then doesn't spend it on its own production that turn. A city feeds one job a turn: the oldest job on its ground first.
- Outside your cities' ground, a unit builds with its own build production only. For now that only matters for bridges.
- When the progress reaches the cost, the improvement or bridge is finished at once.
- Moving the unit, attacking with it or starting another job stops the job, and its progress is lost.
- A job whose site no longer allows it (the city whose work area it was in was captured, or someone bridged that river first) stops.

## Work areas

Each city works the tiles around it, up to the work area radius under "Game numbers". Cities never share a tile, because the minimum distance between cities is more than twice the radius. Improvements can only be built in your own work areas, and your cities collect what they yield.

## Improvements

A builder builds an improvement on the tile it stands on, in one of your cities' work areas (not on the city's own tile). A tile holds one improvement. From the end of the turn it is finished, the city whose work area it is in adds its yield to your stockpile every turn.

| Improvement | Terrain | Cost | Yield per turn |
|---|---|---|---|
| Farm | Plains | 4 | 2 Food |
| Lumber camp | Forest | 4 | 2 Wood |
| Mine | Hills | 6 | 2 Stone |

- **Cost:** the production the job needs (see "Building with units").
- You see an improvement while you can see its tile.

## Bridges

A unit with Build bridges can bridge the river between its tile and a neighbouring tile, from either bank.
- The river edge must not already have a bridge, the far tile must be one a land unit can enter, and neither tile may be in an enemy city's work area.
- A bridged edge costs nothing to cross, even over a deep river, and gives the defender no river bonus.
- Bridges cost the "Bridge cost" under "Game numbers" and belong to nobody: both players can use them. There is no way to destroy one yet.
- You see a bridge while you can see the tiles on both banks.

## City buildings

Each city can build each building once. A city produces either a unit or a building: picking one replaces the other, and the progress is kept. When the progress reaches the building's cost, the building is finished, progress starts again from 0 and the city goes back to the unit it was producing before.

| Building | Cost | Yield per turn | Extra production | Needs next to the city |
|---|---|---|---|---|
| Army camp | 8 | — | +1 | — |
| Campus | 8 | 2 Science | — | — |
| Trade post | 8 | 2 Gold | — | — |
| Culture centre | 8 | 2 Culture | — | — |
| Harbour | 8 | 2 Food | — | Lake |

- **Yield per turn:** added to your stockpile at the end of every one of your turns, from the turn after the building is finished.
- **Extra production:** added to the city's production per turn, for its own production and for jobs on its ground.
- For now buildings only give their yield. Later, trade posts will train traders, army camps will speed up army production and harbours will build ships.

## Stockpile

Your resources (food, wood, stone, gold, science, culture) are counted in the top bar. Nothing spends them yet. You never see your opponent's stockpile, improvements or bridges outside your sight, or what their units are building.

## Fog of war

- You see every tile within sight of your units and cities. Tiles you have seen before stay explored: you see their terrain, but not what is on them now.
- You see enemy units and cities only on tiles you can see right now.
- You see the health of every enemy unit you can see.
- You never see an enemy unit's movement left or job, what an enemy city is producing, or which buildings it has.
- The order in which units and cities are listed, and their ids, reveal nothing about when they were created.

## Game numbers

| Number | Value |
|---|---|
| Players | 2 |
| Map width | 20 |
| Map height | 14 |
| Starting units | Settler, Soldier |
| City sight | 2 |
| Minimum distance between cities | 3 |
| Production per turn | 1 |
| Work area radius | 1 |
| Bridge cost | 6 |
| First unit a new city produces | Soldier |
| Capturing a city resets its production | Yes |
| Full health | 100 |
| Damage between equal units | 30 |
| Extra damage per point of strength difference | 2 |
| Least damage | 5 |
| Most damage | 100 |
| Random spread of damage | ±20% |
| Healing per turn of rest | 10 |
| Healing per turn of rest in your city | 20 |

## Maintaining this page

For maintainers: every change to the game's rules updates this page in the same PR. The tables under "Terrain types", "Rivers", "Unit types", "Improvements", "City buildings", "Class counters" and "Game numbers" are checked against the default ruleset by `RulesReferenceTests` (`tests/Govern.ArchitectureTests`). The first column is the name from the ruleset's `texts`, or the row label listed in the test; a ruleset terrain, river, unit, improvement, building or counter without its row, or a new ability without its label in the test, fails it.
