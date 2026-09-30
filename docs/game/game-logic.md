---
title: Game logic (Core algorithms)
tags: [game, core, rules, algorithms, engine, turns, movement, pathfinding, combat, cities, production, victory, fog-of-war, visibility, determinism]
code: [src/Govern.Core]
updated: 2026-09-30
---

# Game logic: Core algorithms

**Status: draft, under discussion.** This describes, as simple step-by-step algorithms, what `Govern.Core` does today. Every number comes from the game's ruleset and is written as its key in brackets, for example [`units.*.movement`]; the keys are described in [game data](game-data.md). Core reads all of them from the ruleset (`Govern.Core/Rulesets`); none is a constant in code. The implementation details (classes, tests, JSON) are in [game-core](../features/game-core.md).

## Terms

| Term | Meaning |
|---|---|
| Hex, coordinate | A pointy-top hexagon addressed by axial `(q, r)`. The map is a rectangle in odd-r offset layout: tile index = `row × width + column` |
| Neighbours, distance | Each hex has 6 neighbours; distance is the number of hex steps |
| Player | A seat in the game: active, eliminated or resigned |
| Current player | The one player who may act now (turns alternate) |
| Round (turn number) | Increases by 1 each time play wraps around to the first player |
| Unit | Belongs to a player, stands on one tile, has a type and movement left this turn |
| City | Belongs to a player, stands on one tile, produces one unit type at a time |
| Explored / visible | A tile a player has ever seen / sees right now |

## Creating a game

Input: the players (in turn order) and a secret random seed.

1. Check the players: exactly [`setup.players`], all distinct.
2. Create the map: [`map.defaultWidth`] × [`map.defaultHeight`] (each within [`map.minSize`]–[`map.maxSize`]), every tile [`map.defaultTerrain`].
3. Start the random generator from the seed.
4. For each player, in order:
   1. Pick the spawn zone: the first player's zone is the left [`map.spawn.zoneColumnsPerMille`] of the columns, the second's the right one.
   2. Pick a random column in the zone and a random row, keeping [`map.spawn.edgeMargin`] tiles from the edges.
   3. Place the [`setup.startingUnits`] in order: the first on the spawn tile, the k-th k columns from it towards the map centre on the same row (by default a settler, with a soldier next to it). Each unit starts with full movement [`units.*.movement`] and a new random id (see [Randomness and ids](#randomness-and-ids)).
5. Set round 1, the current player to the first player, status "in progress".
6. Update what every player has explored (see [Fog of war](#fog-of-war)).

## Executing a command

Every player action is a command sent by one player (the actor). The engine runs these steps; any failed check stops the command and returns its error code, and nothing changes.

1. **Guard:**
   1. The game is finished → `game.finished`.
   2. The actor isn't an active player → `game.not_a_player`.
   3. The actor isn't the current player (and the command isn't Resign) → `game.not_your_turn`.
2. **Apply** the command's own rule (below).
3. **Victory:** run [Elimination and victory](#elimination-and-victory).
4. **Explore:** update every active player's explored tiles.
5. **Count:** increase the state's internal version by 1 (never shown to players).

### Move a unit

1. The unit must be the actor's → otherwise `unit.not_found` (an enemy unit is reported the same way, so probing ids reveals nothing).
2. It must have movement left → `unit.no_movement`.
3. If the target holds a unit **the actor can see** → `move.occupied`. A hidden blocker is not revealed; it just makes the tile unreachable.
4. Find the cheapest paths (Dijkstra) from the unit's tile, spending at most its movement left:
   - a tile can be entered if it is on the map, its terrain is passable [`terrains.*.isPassable`], no unit stands on it, and no enemy city stands on it;
   - entering a tile costs its terrain's [`terrains.*.movementCost`];
   - equal costs are broken by row, then column, so the path is always the same.
5. The target isn't among the reachable tiles → `move.unreachable`.
6. Move the unit to the target and subtract the path's cost from its movement.

### Attack

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `attack` ability [`units.*.abilities`] → `unit.cannot_attack`.
3. It must have movement left → `unit.no_movement`.
4. The target must be on the map and a neighbour → `attack.not_adjacent`.
5. The target must hold a unit or a city → `attack.no_target`, and it must not be the actor's own → `attack.own_target`.
6. **If a unit stands there:** that unit is destroyed. The attacker stays where it is, and its movement drops to 0.
7. **Otherwise (an undefended city):** the city is captured: its owner becomes the actor, its production progress resets to 0 when [`cities.captureResetsProduction`] is true (it is by default), and the attacker moves into it with movement 0.

### Found a city

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `found_city` ability [`units.*.abilities`] → `unit.cannot_found_city`.
3. It must have movement left → `unit.no_movement`.
4. No city may stand on its tile → `city.tile_taken`.
5. No city of **any** player may be closer than [`cities.minimumSpacing`] → `city.too_close`.
6. Create the city on the unit's tile: a new random id, the owner, the name from the text [`cities.nameTemplate`] with `{n}` = the owner's city count + 1, producing [`cities.initialProduction`] with progress 0.
7. The founding unit is removed.

### Set production

1. The city must be the actor's → `city.not_found`.
2. The unit type must exist → `city.unknown_unit_type`.
3. The city now produces that type. Its progress is kept.

### End turn

1. **Produce**, for each of the actor's cities:
   1. Add [`cities.productionPerTurn`] to its progress, capped at the unit's [`units.*.productionCost`].
   2. If the progress has reached the cost, find a spawn tile: the city's tile if free, otherwise the first free neighbour (in fixed direction order) that is passable and has no unit and no enemy city.
   3. If a spawn tile exists, create the unit there with a new random id and **0 movement**, and reset the progress to 0. If none exists (the city is surrounded), the progress stays full and the unit appears in a later turn.
2. **Pass the turn** to the next active player in seat order, skipping players who are no longer active. If it wraps past the last seat, the round increases by 1.
3. **Reset movement:** every unit of the new current player gets its full [`units.*.movement`].

### Resign

1. Allowed at any time, even on the opponent's turn (the guard skips the turn check).
2. The actor's status becomes "resigned". Victory decides the rest.

## Elimination and victory

After every successful command:
1. **Eliminate** every active player who has no city and no unit with the `found_city` ability [`units.*.abilities`].
2. **Remove** all units of players who are no longer active (eliminated or resigned).
3. If at most one active player is left, the game is **finished**: the winner is that player, or nobody if none is left. There are no other victory conditions for now.

## Fog of war

1. **Visible tiles** of a player: every tile within [`units.*.sight`] of each of their units, and within [`cities.sight`] of each of their cities.
2. **Explored tiles:** after every command, each active player's newly visible tiles are added to their explored set, which only grows.
3. **The view** a player receives:
   - every tile, as unexplored (no terrain), explored (terrain, as last seen) or visible;
   - their own units and cities, plus enemy units and cities on currently visible tiles;
   - enemy cities without their production, and enemy units without their movement left;
   - units and cities ordered by id, so the order reveals nothing about when they were created;
   - the round, the current player, the status, the winner and each player's status.
4. **The view version** is a fingerprint (SHA-256) of everything in the view. It changes only when something the player can see changes, so it can't be used to count the opponent's hidden actions. A command must quote the version it was based on (the server rejects it with `game.stale_version` if the view changed since).

## View previews (what the client can check)

The client never has the full game state, only its view. From the view alone, Core answers, for one of the player's units:
- **Reachable tiles:** the same Dijkstra as [Move a unit](#move-a-unit), where only visible tiles can be entered.
- **Attack targets:** neighbours holding an enemy unit or city, with the same checks as [Attack](#attack).
- **Can found a city here:** the same checks as [Found a city](#found-a-city).

These answers are **exactly** what the engine would decide, because they share the rules' own checks and because of two fog invariants: every unit sees at least as far as it moves [`units.*.sight` ≥ `units.*.movement`], and a founding unit sees every city closer than the spacing [`sight` ≥ `cities.minimumSpacing` − 1]. The ruleset validator enforces both.

## Randomness and ids

- All randomness comes from one deterministic generator stored in the game state, started from the secret seed. Nothing uses the clock or the system random, so the same seed and commands always give the same game.
- A new unit or city id is a generator draw passed through SHA-256 and retried until it is unique. Ids therefore don't reveal how many things were created, and raw generator output never reaches a player.

## Invariants (always true)

- At most one unit stands on a tile; at most one city stands on a tile.
- A unit's movement left is between 0 and its type's movement.
- A city's progress is between 0 and the cost of what it produces.
- Explored tiles only grow; a finished game accepts no command.

## Future mechanics

Each [roadmap](../plans/game-design-roadmap.md) item adds its algorithm here, in this style, before it is implemented: map generation and terrain layers (G4), yields and resources (G5), river crossing (G6), territory growth (G7), city growth, loyalty and the production queue (G8), combat with health and the counter matrix (G9), builders and building upgrades (G10), science and culture with upkeep (G11), governors (G12, G13), seasons (G14), ships and river navigation (G15).

## Open questions

Tracked in [ruleset open questions](../plans/ruleset-open-questions.md) (questions 10–11 there concern this document), to discuss later.
