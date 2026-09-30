---
title: Game logic (Core algorithms)
tags: [game, core, rules, algorithms, engine, turns, movement, pathfinding, combat, health, damage, counters, ranged, healing, cities, production, victory, fog-of-war, visibility, determinism]
code: [src/Govern.Core]
updated: 2026-09-30
---

# Game logic: Core algorithms

**Status: draft, under discussion.** This describes, as simple step-by-step algorithms, what `Govern.Core` does today. Every number comes from the game's ruleset and is written as its key in brackets, for example [`units.*.movement`]; the keys are described in [game data](game-data.md). Core reads all of them from the ruleset (`Govern.Core/Rulesets`); none is a constant in code. The implementation details (classes, tests, JSON) are in [game-core](../features/game-core.md). The same rules written for players, without keys or algorithms, are the [rules reference](rules.md); a rules change updates both.

## Terms

| Term | Meaning |
|---|---|
| Hex, coordinate | A pointy-top hexagon addressed by axial `(q, r)`. The map is a rectangle in odd-r offset layout: tile index = `row × width + column` |
| Neighbours, distance | Each hex has 6 neighbours; distance is the number of hex steps |
| Player | A seat in the game: active, eliminated or resigned |
| Current player | The one player who may act now (turns alternate) |
| Round (turn number) | Increases by 1 each time play wraps around to the first player |
| Unit | Belongs to a player, stands on one tile, has a type, movement left this turn and damage taken (health = [`combat.maxHealth`] − damage) |
| City | Belongs to a player, stands on one tile, produces one unit type at a time |
| Explored / visible | A tile a player has ever seen / sees right now |

## Creating a game

Input: the players (in turn order) and a secret random seed.

1. Check the players: exactly [`setup.players`], all distinct.
2. Size the map: [`map.defaultWidth`] × [`map.defaultHeight`] (each within [`map.minSize`]–[`map.maxSize`]), every tile [`map.defaultTerrain`] for now.
3. Start the random generator from the seed.
4. For each player, in order:
   1. Pick the spawn zone: the first player's zone is the left [`map.spawn.zoneColumnsPerMille`] of the columns, the second's the right one.
   2. Pick a random column in the zone and a random row, keeping [`map.spawn.edgeMargin`] tiles from the edges.
   3. Place the [`setup.startingUnits`] in order: the first on the spawn tile, the k-th k columns from it towards the map centre on the same row (by default a settler, with a soldier next to it). Each unit starts with full movement [`units.*.movement`] and a new random id (see [Randomness and ids](#randomness-and-ids)).
5. Generate the terrain (see [Map generation](#map-generation)); the tiles of every starting unit stay [`map.defaultTerrain`].
6. Set round 1, the current player to the first player, status "in progress".
7. Update what every player has explored (see [Fog of war](#fog-of-war)).

### Map generation

The map's two halves are always the same: every tile is written together with its **mirror**, the tile at column `width − 1 − column` and row `height − 1 − row` (a half turn around the centre). On a map with an even height (the default, 14) the mirror keeps neighbours neighbours, so both halves play the same; on an odd height they differ slightly along the middle rows. The spawns are not mirrored (step 4 picks each independently), so a player can't work out the opponent's start from their own; the map only narrows it slightly (the opponent's starting tiles and their mirrors are plains, which halves the possible start spots on the default ruleset). Knowing one half also tells a player the terrain of the other: that is the accepted price of a fair map. The generator's draws are hashed (`DeterministicRandom.NextHashed`), so the terrain reveals nothing about the random generator's state.

1. **Reserve** every starting unit's tile and its mirror: they stay [`map.defaultTerrain`].
2. For each entry of [`map.generation.patches`], in order:
   1. The target is [`map.generation.patches.*.sharePerMille`] thousandths of the map's tiles (mirrors included).
   2. Until the target is reached, or after as many seeds as the map has tiles: pick a random tile as a seed and **grow a patch** from it. The patch keeps a list of candidate tiles (first the seed); it takes a random candidate, places the terrain on it and on its mirror when both are still [`map.defaultTerrain`] and not reserved, and adds that tile's neighbours to the candidates. It stops at [`map.generation.patches.*.patchSize`] tiles, at the target, or when no candidate is left.
   3. **Impassable terrain keeps the map connected:** a tile of impassable terrain [`terrains.*.isPassable`] is placed only if every passable tile can still reach every other; otherwise the placement is undone and the next candidate is tried.
3. The generator draws from the game's random generator after the spawns, so the same seed always gives the same map.

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
   - entering a tile costs its terrain's [`terrains.*.movementCost`] (default ruleset: plains 1, forest and hills 2), and a path may not cost more than the movement left, so a unit with 1 movement left can't enter a cost-2 tile;
   - equal costs are broken by row, then column, so the path is always the same.
5. The target isn't among the reachable tiles → `move.unreachable`.
6. Move the unit to the target and subtract the path's cost from its movement.

### Attack

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `attack` or `ranged_attack` ability [`units.*.abilities`] → `unit.cannot_attack`.
3. It must have movement left → `unit.no_movement`.
4. The target must be on the map and within the unit's range: 1 for `attack`, [`units.*.ranged.range`] for `ranged_attack` → `attack.not_adjacent` (melee) or `attack.out_of_range` (ranged).
5. The target must hold a unit or a city → `attack.no_target`, and it must not be the actor's own → `attack.own_target`. A ranged unit's target must hold a unit → `attack.no_target`.
6. **If a unit stands there:** they fight (see [Combat](#combat)). The attacker stays where it is, and its movement drops to 0.
7. **Otherwise (an undefended city, melee only):** the city is captured: its owner becomes the actor, its production progress resets to 0 when [`cities.captureResetsProduction`] is true (it is by default), and the attacker moves into it with movement 0.

### Combat

All integer maths, so the server and the browser get identical numbers.
1. **Base strength:** the attacker's [`units.*.strength`], or its [`units.*.ranged.strength`] when it shoots; the defender's [`units.*.strength`]. A defender with no strength (a civilian) skips to step 6.
2. **Health:** each side fights at `strength × (maxHealth + health) ÷ (2 × maxHealth)` (rounded down) [`combat.maxHealth`]; the loss is the "health" modifier.
3. **Counters:** each side adds the `bonus` of every [`combat.counters`] entry whose `class` is its own class and `against` the opponent's [`units.*.class`], attacking or defending.
4. **Terrain:** the defender adds the [`terrains.*.defenceBonus`] of its tile. Strength never goes below 0.
5. **Damage before the roll** a side takes: `base + (dealer's strength − receiver's strength) × perStrengthPoint`, kept within `[min, max]` [`combat.damage.*`]. A ranged attacker takes none.
6. **Roll:** for each side that can take a varying amount, draw a percentage in `100 ± spreadPercent` from the game's generator (hashed draws, `NextHashed`: damage is shown to both players, and raw draws would expose the generator's state), defender first. Damage = `max(1, raw × percent ÷ 100)`, at most the receiver's health. A civilian defender takes its whole health and deals none.
7. **Apply:** each side's damage is added; a unit at 0 health is removed (`unit_destroyed`); both can be removed. The event `unit_attacked` carries both amounts.

The **forecast** (see [View previews](#view-previews-what-the-client-can-check)) runs steps 1 to 5 and gives each side's damage for the lowest and the highest percent, so a rolled damage always lies within it.

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

1. **Heal:** each of the actor's hurt units whose movement left is still full (it neither moved nor attacked this turn) loses [`combat.healing.perTurn`] damage, or [`combat.healing.inOwnCity`] on one of the actor's cities, down to 0.
2. **Produce**, for each of the actor's cities:
   1. Add [`cities.productionPerTurn`] to its progress, capped at the unit's [`units.*.productionCost`].
   2. If the progress has reached the cost, find a spawn tile: the city's tile if free, otherwise the first free neighbour (in fixed direction order) that is passable and has no unit and no enemy city.
   3. If a spawn tile exists, create the unit there with a new random id and **0 movement**, and reset the progress to 0. If none exists (the city is surrounded), the progress stays full and the unit appears in a later turn.
3. **Pass the turn** to the next active player in seat order, skipping players who are no longer active. If it wraps past the last seat, the round increases by 1.
4. **Reset movement:** every unit of the new current player gets its full [`units.*.movement`].

### Resign

1. Allowed at any time, even on the opponent's turn (the guard skips the turn check).
2. The actor's status becomes "resigned". Victory decides the rest.

## Elimination and victory

After every successful command:
1. **Eliminate** every active player who has no city and no unit with the `found_city` ability [`units.*.abilities`].
2. **Remove** all units of players who are no longer active (eliminated or resigned).
3. If at most one active player is left, the game is **finished**: the winner is that player, or nobody if none is left. There are no other victory conditions for now.

## Fog of war

1. **Visible tiles** of a player: every tile within [`units.*.sight`] of each of their units, and within [`cities.sight`] of each of their cities, each plus the [`terrains.*.sightBonus`] of the tile the unit or city stands on (hills: +1).
2. **Explored tiles:** after every command, each active player's newly visible tiles are added to their explored set, which only grows.
3. **The view** a player receives:
   - every tile, as unexplored (no terrain), explored (terrain, as last seen) or visible;
   - their own units and cities, plus enemy units and cities on currently visible tiles;
   - enemy cities without their production, and enemy units without their movement left (their health is shown);
   - units and cities ordered by id, so the order reveals nothing about when they were created;
   - the round, the current player, the status, the winner and each player's status.
4. **The view version** is a fingerprint (SHA-256) of everything in the view. It changes only when something the player can see changes, so it can't be used to count the opponent's hidden actions. A command must quote the version it was based on (the server rejects it with `game.stale_version` if the view changed since).

## View previews (what the client can check)

The client never has the full game state, only its view. From the view alone, Core answers, for one of the player's units:
- **Reachable tiles:** the same Dijkstra as [Move a unit](#move-a-unit), where only visible tiles can be entered.
- **Attack targets:** tiles in range holding an enemy unit (or, for melee, an enemy city), with the same checks as [Attack](#attack).
- **Combat forecast:** for a target holding an enemy unit, both strengths with their modifiers and the damage range each side takes, from the same calculation as [Combat](#combat).
- **Can found a city here:** the same checks as [Found a city](#found-a-city).

These answers are **exactly** what the engine would decide, because they share the rules' own checks and because of three fog invariants: every unit sees at least as far as it moves [`units.*.sight` ≥ `units.*.movement`], a founding unit sees every city closer than the spacing [`sight` ≥ `cities.minimumSpacing` − 1], and a ranged unit sees every tile in its range [`units.*.ranged.range` ≤ `sight`]. The ruleset validator enforces all three.

## Randomness and ids

- All randomness comes from one deterministic generator stored in the game state, started from the secret seed. Nothing uses the clock or the system random, so the same seed and commands always give the same game.
- A new unit or city id is a generator draw passed through SHA-256 and retried until it is unique. Ids therefore don't reveal how many things were created, and raw generator output never reaches a player.

## Invariants (always true)

- At most one unit stands on a tile; at most one city stands on a tile.
- A unit's movement left is between 0 and its type's movement.
- A unit on the map has health between 1 and [`combat.maxHealth`].
- A city's progress is between 0 and the cost of what it produces.
- Explored tiles only grow; a finished game accepts no command.

## Future mechanics

Each [roadmap](design-roadmap.md) item adds its algorithm here, in this style, before it is implemented: terrain layers (G4, deferred until G5), yields and resources (G5), river crossing (G6), territory growth (G7), city growth, loyalty and the production queue (G8), builders and building upgrades (G10), science and culture with upkeep (G11), governors (G12, G13), seasons (G14), ships and river navigation (G15).

## Open questions

Tracked in the [design roadmap open questions](design-roadmap.md#open-questions). Questions 5 and 6, which concerned this document, were answered with G2: this document stays for developers and names no Core classes, and the player-facing version is the [rules reference](rules.md).
