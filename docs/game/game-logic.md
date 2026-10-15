---
title: Game logic (Core algorithms)
tags: [game, core, rules, algorithms, ships, naval, domain, science, unit-upgrade, building-upgrade, trading, upkeep, resource-cost, supply, shortage, building-health, engine, turns, movement, pathfinding, rivers, bridges, builders, jobs, improvements, buildings, territory, yields, city-stock, river-generation, combat, health, damage, counters, ranged, healing, cities, production, victory, fog-of-war, visibility, game-log, events, determinism]
code: [src/Govern.Core]
updated: 2026-10-05
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

Input: the players (in turn order: the creator, then the others in seat order), a secret random seed and, optionally, a predefined map from the map pool (see [Predefined maps](#predefined-maps)).

1. Check the players: [`setup.minPlayers`] to [`setup.maxPlayers`] of them, all distinct; a predefined map must be made for exactly that many.
2. Size the map: a predefined map keeps its own size and tiles (within [`map.minSize`]–[`map.maxSize`]); otherwise the [`map.sizes`] entry for the number of players, every tile [`map.defaultTerrain`] for now.
3. Start the random generator from the seed.
4. **Three or four players on a generated map:** generate the terrain first (see [Map generation](#map-generation), with no reserved tiles), then plan the spawns:
   1. Each player gets a zone [`map.spawn.zoneColumnsPerMille`] of the columns wide, [`map.spawn.edgeMargin`] tiles from the edges. Three players: the left edge over the full height, then the top and the bottom third of the right edge. Four: the corners, clockwise from the top left (a third of the height each).
   2. A zone's candidates are its tiles whose row of starting units (see step 5) is passable. A zone without one gets its first tile's row cleared to [`map.defaultTerrain`].
   3. Each candidate is worth the yields within 3 tiles, as the pool maps' balance check counts them. The target is the value every zone can come closest to; each zone keeps the candidates within 5% of it (or, when none is, the closest ones) and draws one at random.
5. For each player, in seat order, find the spawn and place the [`setup.startingUnits`] in order: the first on the spawn tile, the k-th k columns from it towards the map centre on the same row (by default a settler, with a soldier next to it). Each unit starts with full movement [`units.*.movement`] and a new random id (see [Randomness and ids](#randomness-and-ids)). The spawn is:
   - on a predefined map, one of that player's spawn candidates, drawn on its own;
   - with three or four players on a generated map, the one planned in step 4;
   - with two players on a generated map, a random tile in the player's zone (the first player's is the left [`map.spawn.zoneColumnsPerMille`] of the columns, the second's the right one), [`map.spawn.edgeMargin`] tiles from the edges.
6. Two players on a generated map: generate the terrain now (see [Map generation](#map-generation)); the tiles of every starting unit stay [`map.defaultTerrain`]. (Two-player games keep this order, so a seed gives the same game as before.) A predefined map is used as it is.
7. Set round 1, the current player to the first player, status "in progress".
8. Update what every player has explored (see [Fog of war](#fog-of-war)).

### Predefined maps

A predefined map is a file `maps/<key>.json` (format, authoring and every check: [maps](maps.md)). It is checked against the ruleset before any game uses it: its size is within [`map.minSize`]–[`map.maxSize`], every passable tile ([`terrains.*.isPassable`]) can reach every other without crossing a river that can't be crossed ([`rivers.*.isCrossable`]), its spawn groups (one per player, [`setup.minPlayers`] to [`setup.maxPlayers`] of them) each leave the row of [`setup.startingUnits`] passable, and the starts are worth about the same. The game keeps its own copy of the map in its state, so a later change to the file never touches a running game.

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
3. **Victory:** run [Elimination and victory](#elimination-and-victory), then end deals that lost a side or their trader and give homeless traders a home ([trading](game-logic-trade.md)), then balance every player's science budget ([science](game-logic-science.md#balancing-the-budget-after-every-command)).
4. **Explore:** update every active player's explored tiles.
5. **Count:** increase the state's internal version by 1 (never shown to players).

### Move a unit

1. The unit must be the actor's → otherwise `unit.not_found` (an enemy unit is reported the same way, so probing ids reveals nothing).
2. A trader carrying a deal → `trade.on_deal`; a unit with a job (it is building) → `unit.busy`, so a stray click never stops a build ([Cancel a job](#cancel-a-job)).
3. It must have movement left → `unit.no_movement`.
4. If the target holds a unit **the actor can see** → `move.occupied`. Traders never hold a tile: a trader may enter any tile, and any unit may enter a tile where only traders stand ([sharing tiles](game-logic-trade.md#sharing-tiles)). A hidden blocker is not revealed; it just makes the tile unreachable.
5. Find the cheapest paths (Dijkstra) from the unit's tile, spending at most its movement left:
   - a tile can be entered if it is on the map, its terrain is the unit's ground, no unit stands on it, and no enemy city stands on it. The ground depends on the unit's [`units.*.domain`]: a land unit needs passable terrain [`terrains.*.isPassable`], a naval unit water [`terrains.*.isWater`] (so a ship never enters a city, which stands on land);
   - a step from one tile to a neighbour costs the entered tile's [`terrains.*.movementCost`] (default ruleset: plains 1, forest and hills 2) plus, when a river runs along the edge between them and the edge has no bridge, its [`rivers.*.crossingCost`] (default: stream 0, river 1); a river whose [`rivers.*.isCrossable`] is false (the deep river) can't be stepped over unless the edge is bridged. Rivers don't affect a naval unit's step;
   - a path may not cost more than the movement left, so a unit with 1 movement left can't enter a cost-2 tile;
   - equal costs are broken by row, then column, so the path is always the same.
6. The target isn't among the reachable tiles → `move.unreachable`.
7. Move the unit to the target and subtract the path's cost from its movement. Its destination and its exploring, if any, are dropped.

### Cancel a job

`cancel_job`: the unit must be the actor's (`unit.not_found`) and have a job (`unit.no_job`). The job is dropped with its progress; the movement it took when it started stays spent. No event: the job was never shown to the opponent.

### Set a destination (auto moves)

A unit can be given a destination it walks to on its own, turn after turn (F19). Any other command on the unit (move, attack, start a job, explore) drops the destination.

1. The unit must be the actor's → `unit.not_found`. A null target cancels the order, and the unit's exploring (nothing else changes). A unit with a job → `unit.busy`.
2. If the target holds a unit **the actor can see** → `move.occupied` (never for a trader, and a tile with only traders is free).
3. **Plan** the path over the actor's knowledge only, so it reveals nothing hidden: Dijkstra by total movement cost, without the one-turn limit, from the unit's tile to the target, where a step:
   - enters a tile the actor has explored (or sees), on the map, with terrain of the unit's ground (as in a move);
   - doesn't enter a tile the actor sees with a unit or an enemy city on it (hidden units and cities are not known, so they don't block the plan);
   - costs what a move step costs, with the river the actor knows of on the edge (rivers are known on explored tiles) unless they see a bridge on it (bridges are known only on visible tiles);
   - is skipped when it costs more than the unit's full [`units.*.movement`], since the unit could never take it;
   - breaks equal costs by row, then column.
   No path (off the map, the unit's own tile, unexplored, impassable, walled off) → `move.no_path`.
4. Set the destination, stop the unit exploring, and **walk** at once (below) with the movement it has left.

**Walking**, when the order is given and at the start of each of the owner's turns (after their movement is restored, units in id order):

1. Plan again as in step 3, so a newly seen blocker or bridge is used. No path → the order ends as `blocked`; already on the target → `arrived` (the JSON values are camelCase: `arrived`, `blocked`, `enemySighted`).
2. Note the tiles in the unit's own sight ([`units.*.sight`] plus the [`terrains.*.sightBonus`] of its tile) that hold an enemy unit or city.
3. Take the path's steps one at a time on the real map, while the next step's real cost fits in the movement left. A unit or enemy city the owner couldn't see on the path always comes into the unit's sight a step before it (sight is at least 1), so step 4 ends the order before the walk reaches it; the real cost of a step can only be lower than planned (a bridge on an explored tile the owner doesn't see).
4. After each step, if the unit's sight holds an enemy unit or city that wasn't in it in step 2, stop; the order ends as `enemySighted`.
5. The steps taken form one `unit_moved` event. Reaching the target ends the order as `arrived`. Each end of an order is a `unit_order_ended` event (unit, owner, type, tile, destination, reason) that only the owner sees.

### Explore

`explore` (F20) sets a unit exploring (`Unit.Exploring`, shown to the owner only): it walks on its own, turn after turn, to land its owner has never seen. Any other command on the unit drops it, and so does a destination with a null target (Stop).

1. The unit must be the actor's → `unit.not_found`; a trader carrying a deal → `trade.on_deal`; a unit with a job → `unit.busy`.
2. **Find the nearest frontier** over the actor's knowledge, with the step costs of [Set a destination](#set-a-destination-auto-moves) step 3 (Dijkstra by total cost, ties by row, then column): the first tile, other than the unit's own, that has a neighbour on the map the actor has neither explored nor sees now. None → `move.nothing_to_explore`.
3. Drop the unit's destination, set it exploring and **walk** at once with the movement it has left.

**Walking an explorer**, when the order is given and at the start of each of the owner's turns (with the units that have a destination, in id order): while it has movement left, find the nearest frontier (step 2) and take the planned path's steps as a destination walk does (steps 2 to 4 of Walking), then look again from where it stands, since the steps showed new land. It stops for the turn when the next step costs more than the movement left. All the steps of a turn form one `unit_moved` event. Exploring ends with a `unit_order_ended` event (its destination is the tile the unit stands on) as `explored` when no frontier is left, or as `enemySighted` when a step brings an enemy unit or city into the unit's sight.

### Attack

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `attack` or `ranged_attack` ability [`units.*.abilities`] → `unit.cannot_attack`.
3. It must have movement left → `unit.no_movement`.
4. The target must be on the map and within the unit's range: 1 for `attack`, [`units.*.ranged.range`] for `ranged_attack` → `attack.not_adjacent` (melee) or `attack.out_of_range` (ranged).
5. The target must hold a unit or a city → `attack.no_target` (the unit hit is the tile's holder; with none, another player's trader there, unless the tile is a city, which traders don't defend: [sharing tiles](game-logic-trade.md#sharing-tiles)), and it must not be the actor's own → `attack.own_target`. A ranged unit's target must hold a unit → `attack.no_target`. A melee unit's target must be of its own [`units.*.domain`] → `attack.other_domain`: a land unit can't attack a ship, and a ship can't attack a land unit or a city (a city counts as land); ranged attacks reach both domains. A melee unit can't capture an undefended city across an uncrossable, unbridged river [`rivers.*.isCrossable`] → `attack.river_in_the_way` (it can still fight a unit there).
6. **If a unit stands there:** they fight (see [Combat](#combat)). The attacker stays where it is, its movement drops to 0 and its job, if any, is dropped.
7. **Otherwise (an undefended city, melee only):** the city is captured: its owner becomes the actor, its production progress resets to 0 when [`cities.captureResetsProduction`] is true (it is by default), it stops producing a building (back to its unit), its item is unpaid again (a resource cost the old owner paid is lost), it keeps its buildings, and the attacker moves into it with movement 0.

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
7. The command's name, trimmed and with every run of spaces inside it made one space, must be 1 to 24 characters of letters, digits, spaces, `'`, `-` and `.` → `city.name_invalid`.
8. None of the **actor's own** cities may have that name, ignoring case → `city.name_taken`. Other players' cities don't count, so the rejection never reveals a hidden city's name.
9. Create the city on the unit's tile: a new random id, the owner, that name, producing [`cities.initialProduction`] with progress 0, with the stock [`cities.startingStock`]. It claims every tile within [`territory.initialRadius`] that no city owns.
10. The founding unit is removed.

### Set production

1. The city must be the actor's → `city.not_found`.
2. The unit type must exist → `city.unknown_unit_type`; it must not be locked by science or replaced by a newer unit ([production under locks](game-logic-science.md#production-under-locks)).
3. A trader needs the [`trade.building`] (or an upgrade of it) in the city → `trade.no_trade_post`; a unit with [`units.*.requiresBuilding`] (the ships) needs that building or an upgrade of it → `city.missing_building`.
4. If it already produces that type (and no building), nothing changes.
5. Otherwise, when the resource cost of what it produced was already paid, it goes back to the stock (`ProductionPaid` clears). The city now produces that type, and stops producing a building. Its progress is kept.

### Produce a building

1. The city must be the actor's → `city.not_found`.
2. The building must exist [`buildings`] → `city.unknown_building`.
3. It must not be an upgrade, the city must not have it (or an upgrade of it) yet → `city.building_exists`, and it must not be locked ([production under locks](game-logic-science.md#production-under-locks)).
4. The site (the command's tile) must be on the map, in this city's territory and not the city's own tile → `city.building_not_in_territory`.
5. Its terrain must be passable [`terrains.*.isPassable`] → `city.building_wrong_terrain`.
6. It must have no improvement and no building → `city.building_site_taken`.
7. No unit of the actor may be building an improvement on it → `city.building_site_being_improved`.
8. If the building has [`buildings.*.requiresAdjacentTerrain`], one of the site's neighbours must have one of those terrains → `city.building_needs_terrain`.
9. The same building on the same site changes nothing (no event). If it already produces this building, only the reserved site moves (`City.ProducingBuildingSite`). Otherwise a paid resource cost goes back to the stock, as in [Set production](#set-production), and the city now produces the building on that site instead of its unit. Its progress is kept.

The reservation keeps improvements off the site (`build.tile_has_building`). Setting production to a unit or a capture drops it. When the building is finished (end of turn), it joins `City.Buildings` and is written to the site's `Tile.Building`, and `BuildingCompletedEvent` carries the site. Nothing can make a reserved site invalid: territory only changes hands with its city, so the site needs no new check.

### Build an improvement

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `build_improvement` ability → `unit.cannot_build_improvements`.
3. The improvement must exist [`improvements`] → `build.unknown_improvement`.
4. It must have movement left → `unit.no_movement`.
5. It must not be building this improvement already → `build.already_building`.
6. Its tile must be in the territory of one of the actor's cities and not be a city tile → `build.not_in_territory`; have no improvement → `build.tile_improved`; have no building and not be reserved for one → `build.tile_has_building`; and have one of the improvement's [`improvements.*.terrains`] → `build.wrong_terrain`.
7. The unit's job becomes this improvement with progress 0 (any other job is dropped), and its movement drops to 0.

### Build a bridge

1. The unit must be the actor's → `unit.not_found`.
2. Its type must have the `build_bridge` ability → `unit.cannot_build_bridges`.
3. It must have movement left → `unit.no_movement`.
4. It must not be building this bridge already → `build.already_building`.
5. The target must be a neighbour on the map → `bridge.not_adjacent`; a river must run along the edge between them → `bridge.no_river`; the edge must not be bridged → `bridge.exists`; the target must be passable → `bridge.impassable`; and neither tile may be in an enemy city's territory → `bridge.enemy_territory`.
6. The unit's job becomes this bridge with progress 0 (any other job is dropped), and its movement drops to 0.

### Set the upkeep priority

1. The city must be the actor's → `city.not_found`.
2. The priority must be "buildings first" or "units first" → `city.unknown_upkeep_priority` (an unknown name in the JSON is already a 400).
3. The city pays its upkeep in that order from the next end of turn (`upkeep_priority_set`). New cities pay buildings first.

### Building with units (jobs)

Run at the end of the owner's turn (see [End turn](#end-turn)), for the owner's units that have a job, oldest job first (the round it started, then the unit id):
1. If the job's site no longer allows it (the placement checks of step 6 of [Build an improvement](#build-an-improvement) or step 5 of [Build a bridge](#build-a-bridge) fail, e.g. the city was captured or the edge was bridged from the other bank), the job is dropped.
2. Add the unit's [`units.*.buildProduction`].
3. If the unit stands in the territory of one of the owner's cities and that city has not fed a job yet this turn, also add the city's production per turn ([`cities.productionPerTurn`] plus each of its buildings' [`buildings.*.production`]); the city is then used for this turn and skips its own production.
4. When the progress reaches the cost ([`improvements.*.cost`] or [`bridges.cost`]), the improvement is put on the unit's tile, or the bridge on the edge, and the job ends (`improvement_built`, `bridge_built`). A building upgrade is the third kind of job ([science](game-logic-science.md#upgrade-a-building-a-builders-job)).

### End turn

1. **Resting units:** note the actor's hurt units whose movement left is still full and that have no job (they neither moved, attacked nor worked this turn). They heal in step 4.
2. **Jobs:** advance the actor's unit jobs (see [Building with units](#building-with-units-jobs)).
3. **Income:** each of the actor's cities adds to its stock the tile yield of every tile in its territory, [`cities.yields`] and the yields of its working buildings [`buildings.*.yields`] (a building at 0 health gives nothing), except [`science.resource`], which is never stored ([science](game-logic-science.md)).
4. **Upkeep:** see [Costs and upkeep](#costs-and-upkeep): cities pay for their buildings and the units on their territory, units outside territory live off the land, unpaid items lose health, paid buildings recover, and a supplied resting unit loses [`combat.healing.perTurn`] damage, or [`combat.healing.inOwnCity`] on one of the actor's cities (for a naval unit: next to a tile of the actor's territory holding its [`units.*.requiresBuilding`] or an upgrade of it), down to 0.
5. **Trade:** the deals hosted by the actor's cities swap their goods, then the actor's trading cities move their demand levels (see [trading](game-logic-trade.md#exchange-at-the-end-of-the-host-owners-turn)).
6. **Territory:** each of the actor's cities, by id, whose stock holds the price of its next tile in [`territory.growthResource`] (see [Territory](#territory)) pays it and claims that tile (`territory_grew`).
7. **Produce**, for each of the actor's cities that didn't feed a job this turn, with its production per turn = [`cities.productionPerTurn`] plus each working building's [`buildings.*.production`]:
   1. If the item's resource cost [`units.*.resourceCost`, `buildings.*.resourceCost`] isn't paid yet: when the stock holds all of it, pay it and go on; otherwise the item waits and gains no production this turn.
   2. If it produces a building: add the production; when the progress reaches [`buildings.*.cost`], the city gains the building (`building_completed`) at full health, its progress resets to 0, the next item is unpaid, and it goes back to producing its unit. A new building therefore yields from the next turn.
   3. Otherwise add the production to its progress, capped at the unit's [`units.*.productionCost`].
   4. If the progress has reached the cost, find a spawn tile: the city's tile if free, otherwise the first free neighbour (in fixed direction order) that is passable and has no unit holding it and no enemy city (a trader is born on the city's tile, as it shares tiles). A naval unit is launched instead: the tiles of the city's territory holding its [`units.*.requiresBuilding`] (or an upgrade of it), in row-major order, then each one's neighbours in fixed direction order; the first water tile [`terrains.*.isWater`] with no unit.
   5. If a spawn tile exists, create the unit there with a new random id and **0 movement**, and reset the progress to 0; the next unit is unpaid, and the city goes on with the newest version of that unit it may produce ([science](game-logic-science.md#production-under-locks)). If none exists (the city is surrounded), the progress stays full and the unit appears in a later turn.
8. **Storage:** every city, of every player (the actor's hosted deals deliver to the other side too), cuts its stock to its storage caps: [`cities.storage`] plus the [`buildings.*.storage`] of each of its working buildings, per resource. What is over a cap is lost. Science has no cap (it is never stored). This comes last, so the turn's income can pay upkeep, costs and a tile first.
9. **Pass the turn** to the next active player in seat order, skipping players who are no longer active. If it wraps past the last seat, the round increases by 1.
10. **Reset movement:** every unit of the new current player gets its full [`units.*.movement`].
11. **Walk on:** every unit of the new current player with a destination, or exploring, walks on (see [Set a destination](#set-a-destination-auto-moves); a trader on a route walks its route instead, see [trading](game-logic-trade.md#the-traders-walk)), so the player sees where it got to before acting.

### Costs and upkeep

**Resource cost.** A city pays the whole [`resourceCost`] of what it produces once, at the end of the turn production of it begins (step 7 of [End turn](#end-turn)), and only then does progress grow. A city that changes to a different item gets an already paid cost back; a captured city's item is unpaid again. Bridges and improvements cost only production.

**Upkeep**, step 4 of [End turn](#end-turn), for the actor only:
1. Each of the actor's cities, by id, pays from its stock (after this turn's income) for its buildings, in the ruleset's order, and for the actor's units on its territory, the most hurt first, then by id. With the priority "buildings first" (the default) the buildings come first; with "units first" the units do.
2. Each item's [`buildings.*.upkeep`] or [`units.*.upkeep`] is paid all or nothing: when the stock left holds all of it, it is paid; otherwise nothing is taken and the next item is tried.
3. An unpaid building loses [`upkeep.shortageDamage`] health down to 0 (`building_damaged`); at 0 it gives no yield and no production until it recovers. A paid, hurt building regains [`upkeep.buildingRecovery`].
4. A unit outside the actor's territory (neutral or enemy land) is supplied when the tiles within [`upkeep.supplyRadius`] of it, its own tile included, give at least its upkeep. Their tile yields count whoever owns them, and each unit counts them on its own: two units may live off the same tiles. Nothing is taken from any stock.
5. An unpaid or unsupplied unit loses [`upkeep.shortageDamage`] health but never goes below 1 (`unit_starved`), and doesn't heal. A supplied unit that rested this turn heals (step 4 of [End turn](#end-turn)).

Bridges and improvements have no upkeep. Upkeep is paid before territory growth, so culture spent on borders is what is left after upkeep.

### Resign

1. Allowed at any time, even on another player's turn (the guard skips the turn check).
2. The actor's status becomes "resigned". Victory decides the rest; the resigned player's cities stay where they are, with no units to defend them.

## Elimination and victory

After every successful command:
1. **Eliminate** every active player who has no city and no unit with the `found_city` ability [`units.*.abilities`].
2. **Remove** all units of players who are no longer active (eliminated or resigned).
3. If at most one active player is left, the game is **finished**: the winner is that player, or nobody if none is left. There are no other victory conditions for now.
4. Otherwise the game goes on for the active players: every deal whose host city belongs to a player who is no longer active ends (`trade_deal_changed`, ended), and if the current player is no longer active (they resigned during their own turn, or were eliminated), the turn passes as in steps 9 to 11 of [End turn](#end-turn).

### Territory

A new city claims the unowned tiles within [`territory.initialRadius`]. Afterwards, at the end of its owner's turn (step 6 of [End turn](#end-turn)), after upkeep and trade, a city whose stock holds `tileCost + tileCostIncrease × tiles gained so far` [`territory.*`] of [`territory.growthResource`] pays it and claims one more tile. The tile is chosen without randomness among the tiles no city owns that touch the city's territory and are within [`territory.maxRadius`] of the city:
1. the highest total tile yield (all resources added up);
2. then the nearest to the city;
3. then the lowest row, then the lowest column.

If no tile qualifies, nothing is paid and the stock keeps growing. Cities never take tiles from each other, and a captured city keeps its territory, stock and tiles gained.

## Fog of war

1. **Visible tiles** of a player: every tile within [`units.*.sight`] of each of their units, and within [`cities.sight`] of each of their cities, each plus the [`terrains.*.sightBonus`] of the tile the unit or city stands on (hills: +1), and every tile in their cities' territory.
2. **Explored tiles:** after every command, each active player's newly visible tiles, and the tiles next to their cities' territory, are added to their explored set, which only grows. Knowing those neighbours (terrain and rivers never change) gives the rivers on the territory's outer edges, so a city's income can be previewed exactly.
3. **The view** a player receives:
   - every tile, as unexplored (no terrain), explored (terrain, as last seen) or visible; an explored or visible tile also shows the rivers on the edges it stores (rivers never change). An edge stored by an unexplored neighbour shows once that tile is explored. Improvements, buildings standing on tiles, bridges (stored like rivers) and which player's territory a tile is in are shown on visible tiles only, since they change, so an enemy's placed buildings show where the player can see them; which of the player's own cities owns a tile is shown for their own territory only;
   - their own units and cities, plus enemy units and cities on currently visible tiles;
   - enemy cities without their production or their list of buildings, and enemy units without their movement left, job, destination or exploring (their health is shown). The stock, tiles gained and reserved building site of the player's own cities, never the opponent's. A known, accepted leak: a hurt unit heals only when it kept all its movement and has no job, so an opponent who sees a hurt unit on the same tile before and after its owner's turn learns whether it moved, or, when it stayed and didn't heal, that it is working on a job (one bit per hurt, visible unit per turn). A second accepted leak: a visible enemy territory tile shows that an enemy city is within [`territory.maxRadius`], and watching when enemy borders grow shows roughly that city's culture income (and so whether it has a culture building). A fourth accepted leak: auto moves walk at the start of their owner's turn, inside the opponent's End turn command, so the opponent sees in that update and its log that the visible units which moved are on standing orders, can guess where they are heading, and can tell that their own unit was seen when such a walk stops as it comes into sight; an explorer heads for the nearest land its owner never saw, so its route also hints at where that land lies. A third accepted leak: a unit that loses health without a fight, or doesn't heal while resting, at the end of its owner's turn shows that it was not supplied, so the land around it, or the stock of the city whose territory it stands on, is short;
   - units and cities ordered by id, so the order reveals nothing about when they were created;
   - the trade deals they are a side of, their traders' home cities and the enemy cities with a known trade post they can trade with (see [what each side sees](game-logic-trade.md#what-each-side-sees));
   - their own science per turn and science items, never the opponent's (see [science](game-logic-science.md#what-each-player-sees));
   - the round, the current player, the status, the winner and each player's status.
4. **The view version** is a fingerprint (SHA-256) of everything in the view. It changes only when something the player can see changes, so it can't be used to count the opponent's hidden actions. A command must quote the version it was based on (the server rejects it with `game.stale_version` if the view changed since).
5. **The game log** of a player: after every accepted command, the events of that command they may see, in order. The **seen tiles** are the tiles they could see before the command plus those they see after it. Everything about their own units and cities is kept. Of the other player's events they keep: the end of a turn, a resignation, an elimination and the win; a move cut to the seen tiles of its path (dropped when none is seen); an attack when either tile is seen or the attacked unit is theirs (an attacker on an unseen tile keeps only its owner and type, not its id or tile); a capture when the city's tile is seen or the city was theirs; a destroyed unit, a founded city, a new unit, a finished improvement or a grown territory tile when its tile is seen (a new unit and a grown tile without the city they came from), and a bridge when the builder's end is seen. Production, jobs started, buildings finished or damaged, the upkeep order and starving units are never kept for the other player. Trade events are kept by the two sides of their deal only; a science item's change by its player only; a unit or building upgrade when its tile is seen (without the city for the other player). Hidden events are dropped, never counted, and an entry has no version or time, so the log tells nothing the two views don't.

6. **Accepted leak on a pool map:** the spawn candidates are public, so a player knows the opponent started on the mirror of one of the map's candidates (at least 2, The Basin 3), against about half of all start spots on a generated map. The two draws are independent, so a player's own start tells them nothing about which one.

## View previews (what the client can check)

The client never has the full game state, only its view. From the view alone, Core answers, for one of the player's units:
- **Reachable tiles:** the same Dijkstra as [Move a unit](#move-a-unit), where only visible tiles can be entered; both ends of a step are then visible, so the view knows the river between them.
- **Attack targets:** tiles in range holding an enemy unit (or, for melee, an enemy city), with the same checks as [Attack](#attack).
- **Combat forecast:** for a target holding an enemy unit, both strengths with their modifiers and the damage range each side takes, from the same calculation as [Combat](#combat).
- **Can found a city here:** the same checks as [Found a city](#found-a-city).
- **Can build this improvement here**, **the tiles it can bridge to** and **can this city produce this building:** the same checks as [Build an improvement](#build-an-improvement), [Build a bridge](#build-a-bridge) and [Produce a building](#produce-a-building).
- **A city's income:** for one of the player's own cities, what step 3 of [End turn](#end-turn) will add to its stock (its territory is always visible to its owner).
- **A city's forecast:** its income, the upkeep it will pay, the buildings and units it can't pay for (step 4), what is still missing before its item can begin (step 7.1), and the production its item gains (from the buildings still working after step 4; none while it can't begin). Costs never use [`territory.growthResource`], so territory growth between the two steps can't change the answer. The last two assume the city's production doesn't go to a job this turn; the client checks that from the units' jobs (step 3 of [Building with units](#building-with-units-jobs)). The forecast leaves out the trade of step 5: a deal the city hosts swaps only when the other city has its part, which the player can't see.
- **Destination path:** the path and the turns a walk to a destination needs, planned over exactly the knowledge the view holds (explored terrain and rivers, bridges, units and cities on visible tiles), which is what [Set a destination](#set-a-destination-auto-moves) plans over; a path exists exactly when the order would be accepted (none for a unit with a job).
- **Explore:** whether [Explore](#explore) would be accepted, its frontier search run over exactly the knowledge the view holds (a tile counts as never seen when the view shows it unexplored).
- **A unit's supply:** which own city keeps it up, or, outside territory, what the land around it lacks.
- **Science:** whether a node can be unlocked, a unit upgraded or a building upgrade started, with the same checks as [science](game-logic-science.md#science-previews).
- **Trade:** whether a trader can open a route, a deal can be proposed, and a proposal accepted (after cancelling given deals), with the same checks as the [trade commands](game-logic-trade.md#the-trade-commands).

These answers are **exactly** what the engine would decide, because they share the rules' own checks and because of three fog invariants: every unit sees at least as far as it moves [`units.*.sight` ≥ `units.*.movement`], a founding unit sees every city closer than the spacing [`sight` ≥ `cities.minimumSpacing` − 1], a ranged unit sees every tile in its range [`units.*.ranged.range` ≤ `sight`], and a unit with upkeep sees every tile that supplies it and the rivers on their edges [`sight` ≥ `upkeep.supplyRadius` + 1]. The ruleset validator enforces all four. The territory checks (founding, improvements, bridges) look only at the unit's own tile and a neighbour, which it always sees. A building's site may be any tile of the city's territory, and its check reads the tile's neighbours too: that is exact because the player's own territory is always visible (with its improvements, buildings and units) and the tiles around it are always explored (step 2 of [Fog of war](#fog-of-war)), so their terrain is known. Dropping that explored ring would break the preview and let `city.building_needs_terrain` reveal an unexplored tile's terrain.

## Randomness and ids

- All randomness comes from one deterministic generator stored in the game state, started from the secret seed. Nothing uses the clock or the system random, so the same seed and commands always give the same game.
- A new unit or city id is a generator draw passed through SHA-256 and retried until it is unique. Ids therefore don't reveal how many things were created, and raw generator output never reaches a player.

## Invariants (always true)

- At most one unit stands on a tile; at most one city stands on a tile.
- A unit's movement left is between 0 and its type's movement.
- A unit on the map has health between 1 and [`combat.maxHealth`]; upkeep never kills it.
- A building has health between 0 and [`upkeep.buildingHealth`].
- A city's progress is between 0 and the cost of what it produces.
- A tile belongs to at most one city's territory, and a city's own tile is in its territory.
- A tile has at most one improvement, a city each building at most once, an edge at most one bridge, and only river edges have bridges.
- Explored tiles only grow; a finished game accepts no command.

## Future mechanics

Each [roadmap](design-roadmap.md) item adds its algorithm here, in this style, before it is implemented: terrain layers (G4, deferred until G5), city growth, housing, loyalty and the production queue (G8b), pillage, repair and the other buildings' own mechanics (G10b; the trade post's is [trading](game-logic-trade.md), G17; building upgrades came with [science](game-logic-science.md), G11a), the culture tree (G11b), governors (G12, G13), seasons (G14), carrying units, seas and river navigation (G15b; ships themselves came with G15a, in the steps above).

## Open questions

Tracked in the [design roadmap open questions](design-roadmap.md#open-questions). Questions 5 and 6, which concerned this document, were answered with G2: this document stays for developers and names no Core classes, and the player-facing version is the [rules reference](rules.md).
