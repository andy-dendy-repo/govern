---
title: Game logic (Core algorithms)
tags: [game, core, rules, algorithms, engine, turns, movement, pathfinding, rivers, bridges, builders, jobs, improvements, buildings, territory, yields, city-stock, river-generation, combat, health, damage, counters, ranged, healing, cities, production, victory, fog-of-war, visibility, determinism]
code: [src/Govern.Core]
updated: 2026-10-01
---

# Game logic: Core algorithms

**Status: draft, under discussion.** This describes, as simple step-by-step algorithms, what `Govern.Core` does today. Every number comes from the game's ruleset and is written as its key in brackets, for example [`units.*.movement`]; the keys are described in [game data](game-data.md). Core reads all of them from the ruleset (`Govern.Core/Rulesets`); none is a constant in code. The implementation details (classes, tests, JSON) are in [game-core](../features/game-core.md). The same rules written for players, without keys or algorithms, are the [rules reference](rules.md); a rules change updates both.

## Terms

| Term | Meaning |
|---|---|
| Hex, coordinate | A pointy-top hexagon addressed by axial `(q, r)`. The map is a rectangle in odd-r offset layout: tile index = `row × width + column` |
| Neighbours, distance | Each hex has 6 neighbours; distance is the number of hex steps |
| Edge, corner | The side two neighbouring hexes share, and the point three hexes share. A river runs along edges; each tile stores its east, north-east and north-west edges |
| Player | A seat in the game: active, eliminated or resigned |
| Current player | The one player who may act now (turns alternate) |
| Round (turn number) | Increases by 1 each time play wraps around to the first player |
| Unit | Belongs to a player, stands on one tile, has a type, movement left this turn and damage taken (health = [`combat.maxHealth`] − damage) |
| Unit job | What a unit is building (an improvement on its tile, or a bridge towards a neighbour), with its progress and the round it started |
| City | Belongs to a player, stands on one tile, produces one unit type or one building at a time, and has a set of buildings |
| Territory | The tiles a city owns, its own tile included. A tile belongs to at most one city; a captured city keeps its territory |
| Tile yield | What a tile gives a turn: [`terrains.*.yields`], plus per resource the largest [`rivers.*.yields`] of the rivers on its edges, plus its [`improvements.*.yields`] |
| City stock | Each city's own resource amounts, in [`resources`] order |
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
3. Lay down the rivers (see [Rivers](#rivers)).
4. The generator draws from the game's random generator after the spawns, so the same seed always gives the same map.

### Rivers

Rivers run along edges and are mirrored like the terrain: an edge is written together with the edge between the mirrors of its two tiles. Up to [`map.generation.rivers.count`] times (with a bounded number of attempts):
1. **Source:** pick a random tile whose terrain is in [`map.generation.rivers.sources`], a random one of its six edges and a random one of that edge's two corners to flow towards.
2. **Walk:** the next edge shares that corner: it is one of the two other edges meeting there. Keep only those whose next corner is no closer to the source (the sum of the corner's three tiles' distances to the source doesn't drop), whose two tiles are on the map and not both impassable, whose mirror is an edge too, and which neither this river nor another (or their mirrors) already uses. Take one at random; stop the river when none is left.
3. **Mouth:** the river ends at the first corner that touches a tile in [`map.generation.rivers.mouths`] or the map's border. A river that has no mouth after [`map.generation.rivers.maxLength`] edges, or is shorter than [`map.generation.rivers.minLength`], is dropped.
4. **Kinds:** edge number i (from 0 at the source) gets the river of the last [`map.generation.rivers.stages`] entry with `fromSegment` ≤ i, so rivers deepen downstream. An edge of an uncrossable kind [`rivers.*.isCrossable`] is laid only if every passable tile can still reach every other without crossing an uncrossable edge; otherwise it gets the last crossable kind before it.

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
   - a step from one tile to a neighbour costs the entered tile's [`terrains.*.movementCost`] (default ruleset: plains 1, forest and hills 2) plus, when a river runs along the edge between them and the edge has no bridge, its [`rivers.*.crossingCost`] (default: stream 0, river 1); a river whose [`rivers.*.isCrossable`] is false (the deep river) can't be stepped over unless the edge is bridged;
   - a path may not cost more than the movement left, so a unit with 1 movement left can't enter a cost-2 tile;
   - equal costs are broken by row, then column, so the path is always the same.
5. The target isn't among the reachable tiles → `move.unreachable`.
6. Move the unit to the target and subtract the path's cost from its movement. Its job, if any, is dropped.

### Attack

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `attack` or `ranged_attack` ability [`units.*.abilities`] → `unit.cannot_attack`.
3. It must have movement left → `unit.no_movement`.
4. The target must be on the map and within the unit's range: 1 for `attack`, [`units.*.ranged.range`] for `ranged_attack` → `attack.not_adjacent` (melee) or `attack.out_of_range` (ranged).
5. The target must hold a unit or a city → `attack.no_target`, and it must not be the actor's own → `attack.own_target`. A ranged unit's target must hold a unit → `attack.no_target`. A melee unit can't capture an undefended city across an uncrossable, unbridged river [`rivers.*.isCrossable`] → `attack.river_in_the_way` (it can still fight a unit there).
6. **If a unit stands there:** they fight (see [Combat](#combat)). The attacker stays where it is, its movement drops to 0 and its job, if any, is dropped.
7. **Otherwise (an undefended city, melee only):** the city is captured: its owner becomes the actor, its production progress resets to 0 when [`cities.captureResetsProduction`] is true (it is by default), it stops producing a building (back to its unit), it keeps its buildings, and the attacker moves into it with movement 0.

### Combat

All integer maths, so the server and the browser get identical numbers.
1. **Base strength:** the attacker's [`units.*.strength`], or its [`units.*.ranged.strength`] when it shoots; the defender's [`units.*.strength`]. A defender with no strength (a civilian) skips to step 6.
2. **Health:** each side fights at `strength × (maxHealth + health) ÷ (2 × maxHealth)` (rounded down) [`combat.maxHealth`]; the loss is the "health" modifier.
3. **Counters:** each side adds the `bonus` of every [`combat.counters`] entry whose `class` is its own class and `against` the opponent's [`units.*.class`], attacking or defending.
4. **Terrain and river:** the defender adds the [`terrains.*.defenceBonus`] of its tile and, against a melee attack across an unbridged river edge, that river's [`rivers.*.defenceBonus`] (ranged attacks ignore rivers). Strength never goes below 0.
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
6. Its tile must not be in any city's territory → `city.in_territory`.
7. Create the city on the unit's tile: a new random id, the owner, the name from the text [`cities.nameTemplate`] with `{n}` = the owner's city count + 1, producing [`cities.initialProduction`] with progress 0, with the stock [`cities.startingStock`]. It claims every tile within [`territory.initialRadius`] that no city owns.
8. The founding unit is removed.

### Set production

1. The city must be the actor's → `city.not_found`.
2. The unit type must exist → `city.unknown_unit_type`.
3. The city now produces that type, and stops producing a building. Its progress is kept.

### Produce a building

1. The city must be the actor's → `city.not_found`.
2. The building must exist [`buildings`] → `city.unknown_building`.
3. The city must not have it yet → `city.building_exists`.
4. If the building has [`buildings.*.requiresAdjacentTerrain`], one of the city's neighbours must have one of those terrains → `city.building_needs_terrain`.
5. The city now produces the building instead of its unit. Its progress is kept.

### Build an improvement

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `build_improvement` ability → `unit.cannot_build_improvements`.
3. The improvement must exist [`improvements`] → `build.unknown_improvement`.
4. It must have movement left → `unit.no_movement`.
5. It must not be building this improvement already → `build.already_building`.
6. Its tile must be in the territory of one of the actor's cities and not be a city tile → `build.not_in_territory`; have no improvement → `build.tile_improved`; and have one of the improvement's [`improvements.*.terrains`] → `build.wrong_terrain`.
7. The unit's job becomes this improvement with progress 0 (any other job is dropped), and its movement drops to 0.

### Build a bridge

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `build_bridge` ability → `unit.cannot_build_bridges`.
3. It must have movement left → `unit.no_movement`.
4. It must not be building this bridge already → `build.already_building`.
5. The target must be a neighbour on the map → `bridge.not_adjacent`; a river must run along the edge between them → `bridge.no_river`; the edge must not be bridged → `bridge.exists`; the target must be passable → `bridge.impassable`; and neither tile may be in an enemy city's territory → `bridge.enemy_territory`.
6. The unit's job becomes this bridge with progress 0 (any other job is dropped), and its movement drops to 0.

### Building with units (jobs)

Run at the end of the owner's turn (see [End turn](#end-turn)), for the owner's units that have a job, oldest job first (the round it started, then the unit id):
1. If the job's site no longer allows it (the placement checks of step 6 of [Build an improvement](#build-an-improvement) or step 5 of [Build a bridge](#build-a-bridge) fail, e.g. the city was captured or the edge was bridged from the other bank), the job is dropped.
2. Add the unit's [`units.*.buildProduction`].
3. If the unit stands in the territory of one of the owner's cities and that city has not fed a job yet this turn, also add the city's production per turn ([`cities.productionPerTurn`] plus each of its buildings' [`buildings.*.production`]); the city is then used for this turn and skips its own production.
4. When the progress reaches the cost ([`improvements.*.cost`] or [`bridges.cost`]), the improvement is put on the unit's tile, or the bridge on the edge, and the job ends (`improvement_built`, `bridge_built`).

### End turn

1. **Heal:** each of the actor's hurt units whose movement left is still full and that has no job (it neither moved, attacked nor worked this turn) loses [`combat.healing.perTurn`] damage, or [`combat.healing.inOwnCity`] on one of the actor's cities, down to 0.
2. **Jobs:** advance the actor's unit jobs (see [Building with units](#building-with-units-jobs)).
3. **Income:** each of the actor's cities adds to its stock the tile yield of every tile in its territory, [`cities.yields`] and its buildings' [`buildings.*.yields`].
4. **Territory:** each of the actor's cities, by id, whose stock holds the price of its next tile in [`territory.growthResource`] (see [Territory](#territory)) pays it and claims that tile (`territory_grew`).
5. **Produce**, for each of the actor's cities that didn't feed a job this turn, with its production per turn = [`cities.productionPerTurn`] plus each of its buildings' [`buildings.*.production`]:
   1. If it produces a building: add the production; when the progress reaches [`buildings.*.cost`], the city gains the building (`building_completed`), its progress resets to 0 and it goes back to producing its unit. A new building therefore yields from the next turn.
   2. Otherwise add the production to its progress, capped at the unit's [`units.*.productionCost`].
   3. If the progress has reached the cost, find a spawn tile: the city's tile if free, otherwise the first free neighbour (in fixed direction order) that is passable and has no unit and no enemy city.
   4. If a spawn tile exists, create the unit there with a new random id and **0 movement**, and reset the progress to 0. If none exists (the city is surrounded), the progress stays full and the unit appears in a later turn.
6. **Pass the turn** to the next active player in seat order, skipping players who are no longer active. If it wraps past the last seat, the round increases by 1.
7. **Reset movement:** every unit of the new current player gets its full [`units.*.movement`].

### Resign

1. Allowed at any time, even on the opponent's turn (the guard skips the turn check).
2. The actor's status becomes "resigned". Victory decides the rest.

## Elimination and victory

After every successful command:
1. **Eliminate** every active player who has no city and no unit with the `found_city` ability [`units.*.abilities`].
2. **Remove** all units of players who are no longer active (eliminated or resigned).
3. If at most one active player is left, the game is **finished**: the winner is that player, or nobody if none is left. There are no other victory conditions for now.

### Territory

A new city claims the unowned tiles within [`territory.initialRadius`]. Afterwards, at the end of its owner's turn (step 4 of [End turn](#end-turn)), a city whose stock holds `tileCost + tileCostIncrease × tiles gained so far` [`territory.*`] of [`territory.growthResource`] pays it and claims one more tile. The tile is chosen without randomness among the tiles no city owns that touch the city's territory and are within [`territory.maxRadius`] of the city:
1. the highest total tile yield (all resources added up);
2. then the nearest to the city;
3. then the lowest row, then the lowest column.

If no tile qualifies, nothing is paid and the stock keeps growing. Cities never take tiles from each other, and a captured city keeps its territory, stock and tiles gained.

## Fog of war

1. **Visible tiles** of a player: every tile within [`units.*.sight`] of each of their units, and within [`cities.sight`] of each of their cities, each plus the [`terrains.*.sightBonus`] of the tile the unit or city stands on (hills: +1), and every tile in their cities' territory.
2. **Explored tiles:** after every command, each active player's newly visible tiles, and the tiles next to their cities' territory, are added to their explored set, which only grows. Knowing those neighbours (terrain and rivers never change) gives the rivers on the territory's outer edges, so a city's income can be previewed exactly.
3. **The view** a player receives:
   - every tile, as unexplored (no terrain), explored (terrain, as last seen) or visible; an explored or visible tile also shows the rivers on the edges it stores (rivers never change). An edge stored by an unexplored neighbour shows once that tile is explored. Improvements and bridges (stored like rivers) and which player's territory a tile is in are shown on visible tiles only, since they change; which of the player's own cities owns a tile is shown for their own territory only;
   - their own units and cities, plus enemy units and cities on currently visible tiles;
   - enemy cities without their production or buildings, and enemy units without their movement left or job (their health is shown). The stock and tiles gained of the player's own cities, never the opponent's. A known, accepted leak: a hurt unit heals only when it kept all its movement and has no job, so an opponent who sees a hurt unit on the same tile before and after its owner's turn learns whether it moved, or, when it stayed and didn't heal, that it is working on a job (one bit per hurt, visible unit per turn). A second accepted leak: a visible enemy territory tile shows that an enemy city is within [`territory.maxRadius`], and watching when enemy borders grow shows roughly that city's culture income (and so whether it has a culture building);
   - units and cities ordered by id, so the order reveals nothing about when they were created;
   - the round, the current player, the status, the winner and each player's status.
4. **The view version** is a fingerprint (SHA-256) of everything in the view. It changes only when something the player can see changes, so it can't be used to count the opponent's hidden actions. A command must quote the version it was based on (the server rejects it with `game.stale_version` if the view changed since).

## View previews (what the client can check)

The client never has the full game state, only its view. From the view alone, Core answers, for one of the player's units:
- **Reachable tiles:** the same Dijkstra as [Move a unit](#move-a-unit), where only visible tiles can be entered; both ends of a step are then visible, so the view knows the river between them.
- **Attack targets:** tiles in range holding an enemy unit (or, for melee, an enemy city), with the same checks as [Attack](#attack).
- **Combat forecast:** for a target holding an enemy unit, both strengths with their modifiers and the damage range each side takes, from the same calculation as [Combat](#combat).
- **Can found a city here:** the same checks as [Found a city](#found-a-city).
- **Can build this improvement here**, **the tiles it can bridge to** and **can this city produce this building:** the same checks as [Build an improvement](#build-an-improvement), [Build a bridge](#build-a-bridge) and [Produce a building](#produce-a-building).
- **A city's income:** for one of the player's own cities, what step 3 of [End turn](#end-turn) will add to its stock (its territory is always visible to its owner).

These answers are **exactly** what the engine would decide, because they share the rules' own checks and because of three fog invariants: every unit sees at least as far as it moves [`units.*.sight` ≥ `units.*.movement`], a founding unit sees every city closer than the spacing [`sight` ≥ `cities.minimumSpacing` − 1], and a ranged unit sees every tile in its range [`units.*.ranged.range` ≤ `sight`]. The ruleset validator enforces all three. The territory checks (founding, improvements, bridges) look only at the unit's own tile and a neighbour, which it always sees.

## Randomness and ids

- All randomness comes from one deterministic generator stored in the game state, started from the secret seed. Nothing uses the clock or the system random, so the same seed and commands always give the same game.
- A new unit or city id is a generator draw passed through SHA-256 and retried until it is unique. Ids therefore don't reveal how many things were created, and raw generator output never reaches a player.

## Invariants (always true)

- At most one unit stands on a tile; at most one city stands on a tile.
- A unit's movement left is between 0 and its type's movement.
- A unit on the map has health between 1 and [`combat.maxHealth`].
- A city's progress is between 0 and the cost of what it produces.
- A tile belongs to at most one city's territory, and a city's own tile is in its territory.
- A tile has at most one improvement, a city each building at most once, an edge at most one bridge, and only river edges have bridges.
- Explored tiles only grow; a finished game accepts no command.

## Future mechanics

Each [roadmap](design-roadmap.md) item adds its algorithm here, in this style, before it is implemented: terrain layers (G4, deferred until G5), resource costs, upkeep and supply (G8a), city growth, housing, loyalty and the production queue (G8b), building upgrades, health, pillage and the buildings' own mechanics (G10b), science and culture with upkeep (G11), governors (G12, G13), seasons (G14), ships and river navigation (G15).

## Open questions

Tracked in the [design roadmap open questions](design-roadmap.md#open-questions). Questions 5 and 6, which concerned this document, were answered with G2: this document stays for developers and names no Core classes, and the player-facing version is the [rules reference](rules.md).
