---
title: Game rules reference (for players)
tags: [game, rules, reference, players, how-to-play, map, terrain, forest, hills, mountains, lake, map-generation, units, movement, attack, cities, production, fog-of-war, victory, resign]
code: [rulesets/default/v2.json, src/Govern.Core, tests/Govern.ArchitectureTests/RulesReferenceTests.cs]
updated: 2026-09-30
---

# Game rules reference

How Govern is played today, written for players. Every number on this page comes from the game's ruleset ([`rulesets/default/v2.json`](../../rulesets/default/v2.json)); the step-by-step algorithms behind each rule, with the ruleset keys, are in [game logic](game-logic.md).

## Goal and victory

Govern is a turn-based strategy game for the number of players under "Game numbers" (two today) on a hex map. The last player standing wins.
- A player who has no city and no unit that can found a city is **eliminated**, and their remaining units disappear.
- A player can **resign** at any time, even during the opponent's turn. Their units disappear and the opponent wins.
- There are no other victory conditions yet.

## The map

- The map is a rectangle of hexagonal tiles. Each tile has 6 neighbours, and distances are counted in hex steps.
- A new game's map size is in the table under "Game numbers". Each new map is different: mostly plains, with patches of forest, hills, mountains and lakes (see "Terrain types").
- **Both halves of the map are the same.** The map turned half a turn around its centre looks exactly like itself, so neither player gets the better side. Once you have explored your own half you know the terrain of your opponent's half too. Starts are not mirrored, though: the map only hints that your opponent started on plains in their zone.
- Mountains and lakes never cut the map in two: every tile you can walk on can be reached from every other.
- Each player starts near their own edge of the map (the first player on the left, the second on the right) with the starting units listed under "Game numbers", placed side by side on plains.

## Terrain types

A tile's terrain decides how many movement points it costs to enter, whether land units can enter it at all, and whether a unit or city standing on it sees further.

| Terrain | Movement cost | Passable | Sight bonus |
|---|---|---|---|
| Plains | 1 | Yes | — |
| Forest | 2 | Yes | — |
| Hills | 2 | Yes | +1 |
| Mountains | — | No | — |
| Lake | — | No | — |

- **Sight bonus:** a unit or city standing on the tile sees this many tiles further than its own sight.
- Forest and hills don't give defence yet; defence comes with health and combat (G9).
- The board's **Movement cost** lens (the toolbar at the top left of the map) writes each known tile's cost on it, and ✕ where land units can't go.

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
- **Sight:** how many tiles away the unit can see, plus the sight bonus of the terrain it stands on.
- **Production cost:** production points a city needs to build the unit.
- **Found city:** the unit can turn into a city (see "Founding a city").
- **Attack:** the unit can attack an adjacent enemy unit or city (see "Attacking").

## Moving a unit

- A unit moves along the cheapest path to a tile it can reach with the movement it has left. The movement spent is the path's total cost.
- A step can't cost more than the movement left: a unit with 1 movement left can't enter forest or hills that turn.
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
- Cities see as far as the city sight under "Game numbers", plus the sight bonus of their tile.

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
