---
title: Game rules reference (for players)
tags: [game, rules, reference, players, how-to-play, map, terrain, forest, hills, mountains, lake, rivers, deep-river, crossing, map-generation, units, scout, recon, builders, improvements, farm, mine, lumber-camp, bridges, buildings, campus, harbour, resources, water, desert, tile-yields, territory, borders, culture, city-stock, resource-cost, upkeep, supply, starvation, building-health, movement, attack, combat, health, damage, counters, ranged, healing, defence, cities, production, fog-of-war, victory, resign]
code: [rulesets/default/v8.json, src/Govern.Core, tests/Govern.ArchitectureTests/RulesReferenceTests.cs]
updated: 2026-10-03
---

# Game rules reference

How Govern is played today, written for players. Every number on this page comes from the game's ruleset ([`rulesets/default/v8.json`](../../rulesets/default/v8.json)); the step-by-step algorithms behind each rule, with the ruleset keys, are in [game logic](game-logic.md).

## Goal and victory

Govern is a turn-based strategy game for the number of players under "Game numbers" (two today) on a hex map. The last player standing wins.
- A player who has no city and no unit that can found a city is **eliminated**, and their remaining units disappear.
- A player can **resign** at any time, even during the opponent's turn. Their units disappear and the opponent wins.
- There are no other victory conditions yet.

## The map

- The map is a rectangle of hexagonal tiles. Each tile has 6 neighbours, and distances are counted in hex steps.
- A new game's map size is in the table under "Game numbers". Each new map is different: mostly plains, with patches of forest, hills, mountains, lakes and desert (see "Terrain types").
- **Both halves of the map are the same.** The map turned half a turn around its centre looks exactly like itself, so neither player gets the better side. Once you have explored your own half you know the terrain of your opponent's half too. Starts are not mirrored, though: the map only hints that your opponent started on plains in their zone.
- Rivers run along the edges between tiles (see "Rivers").
- Mountains, lakes and deep rivers never cut the map in two: every tile you can walk on can be reached from every other.
- Each player starts near their own edge of the map (the first player on the left, the second on the right) with the starting units listed under "Game numbers", placed side by side on plains.

## Terrain types

A tile's terrain decides how many movement points it costs to enter, whether land units can enter it at all, whether a unit or city standing on it sees further, how much it helps a unit defending on it, and what it gives every turn.

| Terrain | Movement cost | Passable | Sight bonus | Defence bonus | Yield per turn |
|---|---|---|---|---|---|
| Plains | 1 | Yes | — | — | 2 Food, 1 Water |
| Forest | 2 | Yes | — | +3 | 1 Food, 1 Water, 2 Wood |
| Hills | 2 | Yes | +1 | +3 | 1 Food, 2 Stone |
| Mountains | — | No | — | — | 2 Stone |
| Lake | — | No | — | — | 1 Food, 3 Water |
| Desert | 1 | Yes | — | — | — |

- **Sight bonus:** a unit or city standing on the tile sees this many tiles further than its own sight.
- **Defence bonus:** a unit attacked on the tile fights with this much extra strength (see "Combat"). Attacking from it gives nothing.
- **Yield per turn:** what the tile gives (see "Tile yields").
- The board's **Movement cost** lens (the toolbar at the top left of the map) writes each known tile's cost on it, and ✕ where land units can't go.

## Rivers

Rivers run along the edges between tiles, not across tiles. Each one starts next to hills or mountains and flows to a lake or the edge of the map, getting deeper as it goes: a stream first, then a river, then a deep river. Like the terrain, rivers are the same on both halves of the map.

| River | Crossing | Defence bonus | Gives the tiles beside it |
|---|---|---|---|
| Stream | Free | — | 1 Water |
| River | +1 | +3 | 1 Water |
| Deep river | Can't cross | +5 | 1 Water |

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
| Scout | Recon | 8 | — | 4 | 4 | 3 | — | — |

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
- **Build improvements:** the unit can build an improvement on its tile in your cities' territory, but not on a city (see "Improvements").
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
- **The scout** can't attack, but it has a strength, so when it is attacked it fights back like any other unit and is not destroyed by the first hit. It is the fastest and farthest-seeing unit, made for exploring.
- **Healing:** at the end of your turn, each of your hurt units that neither moved, attacked nor worked on a job this turn heals, more in one of your cities (under "Game numbers"), up to full health, as long as it gets its upkeep (see "Costs and upkeep").

## Class counters

| Class | Strong against | Bonus |
|---|---|---|
| Melee | Anti-cavalry | +5 |
| Anti-cavalry | Cavalry | +10 |
| Cavalry | Ranged | +5 |

- Ranged units counter melee in their own way: they shoot from two tiles away and take no damage back.
- Recon (the scout) has no counter either way.

## Founding a city

- A unit with the Found city ability that still has movement left can found a city on its own tile. The unit is used up.
- A city can't be founded on another city, closer to any city (yours or the enemy's, seen or not) than the minimum distance under "Game numbers", or on a tile that already belongs to a city's territory.
- A new city owns its own tile and the tiles around it that no city owns yet, up to the radius in "Territory of a new city (radius)" under "Game numbers" (see "Territory"), and starts with a few resources: 5 Food, 5 Water, 5 Wood and 5 Stone.
- A new city is named "City n", where n is the number of cities you own, including the new one. It starts producing the unit in "First unit a new city produces" under "Game numbers".

## Cities and production

- Each city produces one unit type at a time. You can change it at any time during your turn; progress is kept.
- A unit or building also costs resources, which the city pays in full when it starts producing it; until it can pay, it waits (see "Costs and upkeep").
- When you end your turn, each of your cities adds its production per turn (under "Game numbers") to its progress. When the progress reaches the unit's production cost, the unit appears on the city's tile, or on the first free neighbouring tile a unit can stand on if the city's tile is taken, with 0 movement. Progress then starts again from 0.
- If every tile around the city is taken, the finished unit waits and appears in a later turn.
- Cities see as far as the city sight under "Game numbers", plus the sight bonus of their tile.

## Building with units

Builders and soldiers build things with their own **build production** (under "Unit types"). There are no charges: a unit can build as often as you like.
- To start, select the unit and pick what to build in the action bar. Starting uses up the unit's movement for the turn, and the unit needs movement left to start.
- At the end of each of your turns, the job gains the unit's build production. **In your city's territory**, the city's production is added too, and that city then doesn't spend it on its own production that turn. A city feeds one job a turn: the oldest job in its territory first.
- Outside your cities' territory, a unit builds with its own build production only. For now that only matters for bridges.
- When the progress reaches the cost, the improvement or bridge is finished at once.
- Moving the unit, attacking with it or starting another job stops the job, and its progress is lost.
- A job whose site no longer allows it (the city whose territory it was in was captured, or someone bridged that river first) stops.

## Tile yields

Every tile gives resources every turn: its terrain's yield (see "Terrain types"), **plus 1 Water when a river runs along any of its edges** (however many rivers touch it), plus its improvement's yield. Desert gives nothing.
- Inside a city's territory, the city gathers the yield of every tile it owns (see "City resources").
- The board's **Yields** lens writes each known tile's total yield on it, tinted green when rich (4 or more), yellow when poor and red when barren; hover a tile for each resource.

## Territory

Each city owns a set of tiles: its territory. A tile belongs to at most one city, and a captured city keeps its territory.
- A new city owns its own tile and the free tiles around it (see "Founding a city").
- Every city makes 1 Culture a turn on its own. **When a city holds the price of a new tile in Culture**, it pays it at the end of your turn and takes one more tile. The first tile costs the "Price of the first new tile" under "Game numbers", and each tile it has taken since its founding adds the "Price increase per tile gained".
- The new tile is always a free tile next to the territory, no further from the city than the "Largest territory (radius)". The city takes the one with the highest total yield; when several are equal, the nearest to the city, then the top-most, then the left-most. A city with no free tile left keeps its Culture.
- Improvements can only be built in your own cities' territory, and bridges can't touch an enemy city's territory.
- Your cities see all of their territory. You see the borders of enemy territory on tiles you can see.

## City resources

Each city keeps its own resources. At the end of each of your turns, every one of your cities adds to its own stock the yield of every tile in its territory, its own 1 Culture and its buildings' yields, then pays its upkeep (see "Costs and upkeep"). Territory is paid in Culture, and units and buildings in the other resources. The city panel shows a city's resources with what each gains or loses per turn, its upkeep, and what its next tile costs. The top bar shows the total over all your cities.

## Costs and upkeep

Units and buildings cost resources as well as production, and need resources every turn to stay healthy. Improvements and bridges cost only production and need nothing afterwards.

| Item | Resource cost | Upkeep per turn |
|---|---|---|
| Settler | 4 Food, 2 Water | 1 Food, 1 Water |
| Soldier | 2 Food | 1 Food, 1 Water |
| Spearman | 2 Wood | 1 Food, 1 Water |
| Horseman | 3 Food, 2 Water | 2 Food, 2 Water |
| Archer | 2 Wood | 1 Food, 1 Water |
| Builder | 2 Food | 1 Food, 1 Water |
| Scout | 1 Food | 1 Food |
| Army camp | 4 Stone, 2 Wood | 1 Food |
| Campus | 4 Stone | 1 Gold |
| Trade post | 4 Wood | — |
| Culture centre | 3 Stone | 1 Gold |
| Harbour | 6 Wood | — |

- **Resource cost:** paid in full by the city at the end of the turn it starts producing the item; production then adds up until it is done. If the city can't pay yet, the item waits and gains no production; the city panel says what is missing. A city producing the same unit again pays again for each one. Switching to something else before it is done gives the resources back (the production progress is kept, as always). A captured city's paid cost is lost.
- **Upkeep on your territory:** each city pays, from its own stock, for its buildings and for your units standing in its territory. An item is paid in full or not at all. A city pays its buildings first (in the order of the table) and then its units, the most hurt first; a switch in the city panel makes it pay its units first instead.
- **Upkeep outside your territory:** a unit in neutral or enemy land lives off the land: the tiles around it (see "Land that supplies a unit outside your territory" under "Game numbers"), its own included, must together yield at least its upkeep of each resource. Units sharing those tiles don't compete for them: each gets the full yield. Plains and forests feed an army; in deep desert it starves.
- **A unit without upkeep** loses health at the end of your turn instead of healing ("Health lost in a turn without upkeep"), but never drops below 1: hunger alone never kills, it leaves the unit easy to finish off. Once supplied again it heals as usual.
- **A building without upkeep** loses health the same way. At 0 it stays but gives no yield and no production until it is paid again; a paid, hurt building regains health every turn.

## Improvements

A builder builds an improvement on the tile it stands on, in one of your cities' territory (not on a city's own tile, nor on a tile with a building or one reserved for a building). A tile holds one improvement. From the end of the turn it is finished, the city whose territory it is in adds the improvement's yield to its resources every turn, on top of the tile's own yield.

| Improvement | Terrain | Cost | Yield per turn |
|---|---|---|---|
| Farm | Plains | 4 | 2 Food |
| Lumber camp | Forest | 4 | 2 Wood |
| Mine | Hills | 6 | 2 Stone |

- **Cost:** the production the job needs (see "Building with units").
- You see an improvement while you can see its tile.

## Bridges

A unit with Build bridges can bridge the river between its tile and a neighbouring tile, from either bank.
- The river edge must not already have a bridge, the far tile must be one a land unit can enter, and neither tile may be in an enemy city's territory.
- A bridged edge costs nothing to cross, even over a deep river, and gives the defender no river bonus.
- Bridges cost the "Bridge cost" under "Game numbers" and belong to nobody: both players can use them. There is no way to destroy one yet.
- You see a bridge while you can see the bank that holds it (each river edge is kept by one of its two tiles).

## City buildings

Each city can build each building once. A city produces either a unit or a building: picking one replaces the other, and the progress is kept. When the progress reaches the building's cost, the building is finished, progress starts again from 0 and the city goes back to the unit it was producing before.

Every building stands on its own tile of the city's territory, which you pick when you order it:
- The tile must be in that city's territory, not the city's own tile, on terrain a unit can enter (not a lake or mountains), with no improvement and no other building, and no builder of yours working on an improvement there.
- The tile is reserved while the city produces the building: no improvement can be started on it. Ordering the same building again on another tile moves the reservation and keeps the progress.
- The tile keeps its own yield. The building's yield and production go to the city as a whole, wherever it stands.
- A city whose free tiles are all taken can't order a building until its territory grows.
- You see a building on its tile while you can see the tile. When a city is captured its buildings stay on their tiles, and a building it was still producing is dropped.

| Building | Cost | Yield per turn | Extra production | Needs next to its tile |
|---|---|---|---|---|
| Army camp | 8 | — | +1 | — |
| Campus | 8 | 2 Science | — | — |
| Trade post | 8 | 2 Gold | — | — |
| Culture centre | 8 | 2 Culture | — | — |
| Harbour | 8 | 2 Food | — | Lake |

- **Yield per turn:** added to the city's resources at the end of every one of your turns, from the turn after the building is finished.
- **Extra production:** added to the city's production per turn, for its own production and for jobs in its territory.
- Buildings also cost resources and upkeep (see "Costs and upkeep"). For now they only give their yield. Later, trade posts will train traders, army camps will speed up army production and harbours will build ships.

## Resources in the top bar

The top bar counts your resources (food, water, wood, stone, gold, science, culture) over all your cities. You never see your opponent's resources, their improvements, bridges or borders outside your sight, or what their units are building.

## Fog of war

- You see every tile within sight of your units and cities. Tiles you have seen before stay explored: you see their terrain, but not what is on them now.
- You always see every tile of your territory, and you know the terrain and rivers of the tiles right next to it.
- You see which player's territory a tile is in when you can see the tile.
- You see enemy units and cities only on tiles you can see right now.
- You see the health of every enemy unit you can see.
- You never see an enemy unit's movement left or job, what an enemy city is producing, which buildings it has, or its stock. An enemy unit you watch losing health, or not healing, between turns may still tell you that it moved, or that the land or the city keeping it up is short.
- The order in which units and cities are listed, and their ids, reveal nothing about when they were created.
- Your game log lists only what you could see happen: an enemy move appears for the part of its path you saw, an attack from the fog shows the attacker's type but not where it stood, and what happens out of your sight leaves no trace in it.

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
| Territory of a new city (radius) | 1 |
| Largest territory (radius) | 3 |
| Price of the first new tile | 4 culture |
| Price increase per tile gained | 2 |
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
| Land that supplies a unit outside your territory (radius) | 1 |
| Health lost in a turn without upkeep | 10 |
| Full health of a building | 100 |
| Health a building regains per paid turn | 10 |

## Maintaining this page

For maintainers: every change to the game's rules updates this page in the same PR. The tables under "Terrain types", "Rivers", "Unit types", "Improvements", "City buildings", "Class counters" and "Game numbers" are checked against the default ruleset by `RulesReferenceTests` (`tests/Govern.ArchitectureTests`). The first column is the name from the ruleset's `texts`, or the row label listed in the test; a ruleset terrain, river, unit, improvement, building or counter without its row, or a new ability without its label in the test, fails it.
