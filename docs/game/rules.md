---
title: Game rules reference (for players)
tags: [game, rules, reference, players, how-to-play, map, terrain, units, movement, attack, cities, production, fog-of-war, victory, resign]
code: [rulesets/default/v1.json, src/Govern.Core, tests/Govern.ArchitectureTests/RulesReferenceTests.cs]
updated: 2026-09-30
---

# Game rules reference

How Govern is played today, written for players. Every number on this page comes from the game's ruleset ([`rulesets/default/v1.json`](../../rulesets/default/v1.json)); the step-by-step algorithms behind each rule, with the ruleset keys, are in [game logic](game-logic.md).

## Goal and victory

Govern is a turn-based strategy game for the number of players under "Game numbers" (two today) on a hex map. The last player standing wins.
- A player who has no city and no unit that can found a city is **eliminated**, and their remaining units disappear.
- A player can **resign** at any time, even during the opponent's turn. Their units disappear and the opponent wins.
- There are no other victory conditions yet.

## The map

- The map is a rectangle of hexagonal tiles. Each tile has 6 neighbours, and distances are counted in hex steps.
- A new game's map size is in the table under "Game numbers". Every tile is currently plains; more terrain types come with G4.
- Each player starts near their own edge of the map (the first player on the left, the second on the right) with the starting units listed under "Game numbers", placed side by side.

## Terrain types

A tile's terrain decides how many movement points it costs to enter and whether land units can enter it at all.

| Terrain | Movement cost | Passable |
|---|---|---|
| Plains | 1 | Yes |

## Turns and rounds

- Players take turns in seat order. Only the player whose turn it is can act, except for resigning.
- A turn ends when the player ends it. There is no time limit.
- When play wraps around to the first player, the round number goes up by 1. The game starts in round 1.
- At the start of your turn, all your units get their full movement back.

## Unit types

| Unit | Movement | Sight | Production cost | Abilities |
|---|---|---|---|---|
| Settler | 2 | 2 | 6 | Found city |
| Soldier | 2 | 2 | 4 | Attack |

- **Movement:** movement points per turn. Entering a tile costs its terrain's movement cost.
- **Sight:** how many tiles away the unit can see.
- **Production cost:** production points a city needs to build the unit.
- **Found city:** the unit can turn into a city (see "Founding a city").
- **Attack:** the unit can attack an adjacent enemy unit or city (see "Attacking").

## Moving a unit

- A unit moves along the cheapest path to a tile it can reach with the movement it has left. The movement spent is the path's total cost.
- A unit can't enter a tile with another unit on it, an enemy city, or impassable terrain. It can enter your own cities.
- Only one unit can stand on a tile.
- A tile blocked by an enemy unit you can't see is simply unreachable; the game doesn't tell you why, so it reveals nothing about the hidden unit.

## Attacking

- A unit with the Attack ability that still has movement left can attack an enemy unit or city on a neighbouring tile.
- **Attacking a unit** destroys it. The attacker stays where it is, and its movement drops to 0. (Health and counters come with G9.)
- **Attacking a city with no unit on it** captures the city. The attacker moves into it with 0 movement, the city becomes yours, and its production progress resets when "Capturing a city resets its production" is Yes under "Game numbers".
- A city with a unit on it is defended: attack the unit first.

## Founding a city

- A unit with the Found city ability that still has movement left can found a city on its own tile. The unit is used up.
- A city can't be founded on another city, or closer to any city (yours or the enemy's, seen or not) than the minimum distance under "Game numbers".
- A new city is named "City n", where n is the number of cities you own, including the new one. It starts producing the unit in "First unit a new city produces" under "Game numbers".

## Cities and production

- Each city produces one unit type at a time. You can change it at any time during your turn; progress is kept.
- When you end your turn, each of your cities adds its production per turn (under "Game numbers") to its progress. When the progress reaches the unit's production cost, the unit appears on the city's tile, or on the first free neighbouring tile a unit can stand on if the city's tile is taken, with 0 movement. Progress then starts again from 0.
- If every tile around the city is taken, the finished unit waits and appears in a later turn.
- Cities see as far as the city sight under "Game numbers".

## Fog of war

- You see every tile within sight of your units and cities. Tiles you have seen before stay explored: you see their terrain, but not what is on them now.
- You see enemy units and cities only on tiles you can see right now.
- You never see an enemy unit's movement left, or what an enemy city is producing.
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
| First unit a new city produces | Soldier |
| Capturing a city resets its production | Yes |

## Maintaining this page

For maintainers: every change to the game's rules updates this page in the same PR. The tables under "Terrain types", "Unit types" and "Game numbers" are checked against the default ruleset by `RulesReferenceTests` (`tests/Govern.ArchitectureTests`). The first column is the name from the ruleset's `texts`, or the row label listed in the test; a ruleset terrain or unit without its row, or a new ability without its label in the test, fails it.
