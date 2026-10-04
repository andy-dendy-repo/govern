---
title: Predefined maps (the map pool)
tags: [game, maps, map-pool, predefined-map, basin, lobby, map-choice, spawns, asymmetric, fairness, balance, rivers, terrain, validation, authoring]
code: [maps, src/Govern.Core/Map/PredefinedMap.cs, src/Govern.Core/Map/PredefinedMapParser.cs, src/Govern.Core/Map/PredefinedMapValidator.cs, src/Govern.Core/Map/PredefinedMapBalanceValidator.cs, src/Govern.Application/Games/Services/MapPoolService.cs, src/Govern.Client/Features/Games/Services/MapPoolService.cs, src/Govern.Domain/ValueObjects/MapChoice.cs, src/Govern.Client/Features/Design/Pages/MapPreviewPage.razor]
updated: 2026-10-04
---

# Predefined maps (the map pool)

G18 ([plan](../plans/done/G18-predefined-maps.md)). Besides a generated map ([game logic](game-logic.md#map-generation)), a game can be played on a hand-made map from the **map pool**: every file `maps/<key>.json` at the repository root. The game's creator picks the map in the lobby ([games feature](../features/games.md#choosing-the-map-mapchoice)); the player rules are in [rules](rules.md#maps).

## Maps in the pool

| Key | Name | Size | Spawn candidates a side | Idea |
|---|---|---|---|---|
| `basin` | The Basin | 32 × 20 | 3 | Not a mirror (since 2026-10-04): a lake in the centre splits the front into a north and a south flank. The west is a river valley (river near the edge, stream near the ridge) behind a straight ridge with two hill passes, with a forested north flank and a stream. The east is higher land behind a bent ridge with a wide northern gap and one hill pass: a small lake, a river and a stream, more hills and a north-east desert corner, and a stream on the south flank. Its starts (2 tiles from the edge) are worth 112 to 125 (the validator's measure, below); the west yields 72 wood and 96 stone, the east 64 and 108 |

## Map file format

```json
{
  "key": "basin",
  "name": "The Basin",
  "width": 32,
  "height": 20,
  "legend": { ".": "plains", "f": "forest", "h": "hills", "^": "mountains", "~": "lake", "d": "desert" },
  "rows": ["hh....ff.....ddd.......hh..fff..", "..."],
  "rivers": [{ "tile": [6, 10], "side": "northEast", "river": "stream" }],
  "firstSpawns": [[2, 5], [2, 9], [2, 15]],
  "secondSpawns": [[29, 4], [29, 11], [29, 16]]
}
```

| Field | Meaning |
|---|---|
| `key` | Lower snake_case id; the file must be named `<key>.json`. `generated` and `random` are reserved (they are lobby choices) |
| `name` | What players see, 1 to 40 characters |
| `width`, `height` | Within the ruleset's `map.minSize`–`map.maxSize` |
| `legend` | One character per terrain of the ruleset (`terrains.*.id`) |
| `rows` | `height` strings of `width` legend characters, top row first; odd rows are shifted half a tile right (odd-r layout, the same as the board) |
| `rivers` | Each river edge once: the tile that owns it as `[column, row]`, its `side` (`east`, `northEast` or `northWest`, the same three sides `TileRivers` stores) and the ruleset river (`rivers.*.id`) |
| `firstSpawns` | The first player's spawn candidates as `[column, row]`, at least 2, in the left half |
| `secondSpawns` | The second player's spawn candidates, at least 2, in the right half |

The parser is as strict as the ruleset's: camelCase names only, every field required, no unknown fields, no duplicate keys.

## Map validation

`PredefinedMap.Parse(ruleset, json)` reports every problem with its JSON path (`$.rows[3]`, `$.rivers[2].river`, `$.firstSpawns[0]`); `ParseValid` throws `FormatException` listing them. The checks:

| Check | Why |
|---|---|
| Size within the ruleset's limits | The ruleset's bounds |
| Every row the right length, every character in the legend, every legend terrain in the ruleset | The map is built from them |
| Every river edge on the map (the tile across it too), once, with a ruleset river | `TileRivers` stores an edge once |
| Each resource's yield in the left half within 15% of the right half's (terrain yields, and river yields counted in the half of the tile that owns the edge) | Neither player gets the richer half, although the halves differ |
| Every spawn candidate's start value within 10% of the mean of all candidates, and both sides' average within 5% | Every start is about as good. A start's value (`PredefinedMapBalanceValidator.StartValue`) is the sum of all the yields of the tiles within 3 of it and of the rivers between them |
| Every passable tile reachable from every other without crossing a river that can't be crossed | Nobody is walled in |
| Every spawn candidate in its player's half (first: left, second: right), with the row of starting units (the k-th unit k columns towards the centre) on passable tiles | `GameFactory` places the units that way |

`PredefinedMapTests.Parse_EveryPoolMap_IsValidForTheDefaultRuleset` validates every file in `maps/` against the default ruleset.

## How a game uses a pool map

- `GameSetup.Map` carries the map into `GameFactory.Create`: the game's state gets the map's tiles and rivers as they are (no generation), and the seed picks one of the first player's spawn candidates and, on its own, one of the second player's.
- The state keeps its own copy of the map, so editing a map file never changes a running game; pool maps need no versioning (unlike rulesets).
- The Api (`Application/Games/Services/MapPoolService`) and the client (`Client/Features/Games/Services/MapPoolService`) both embed `maps/*.json` and parse them once against the default ruleset, so the lobby's picker offers exactly the maps the Api accepts. An invalid file fails every request that needs the pool until a restart (the load error is cached); `PredefinedMapTests` catches it before that.
- **Accepted leak:** the spawn candidates are public (in the file and the preview), so a player knows the opponent started on one of their side's candidates. The validator requires at least 2 candidates; The Basin has 3. Their own start tells them nothing about which one, because the two draws are independent.
- A fixed map can be learned after a few games: fog of war still hides units and cities, and the random spawn candidates vary the openings. More maps in the pool are the real answer.

## Adding a map

1. Write `maps/<key>.json`. Give each side land of its own, then balance it: a small script that prints each half's yields and each candidate's start value (the measure above) helps (The Basin was tuned that way). A river along a row is a zigzag: the `northEast` and `northWest` edges of each tile of the row below it.
2. Run `dotnet test --project tests/Govern.UnitTests` (the pool test reports every problem).
3. Look at it on the map preview, `/design/maps` (Development only): every tile in sight, with a starting unit on each spawn candidate of both sides (west and east).
4. Add a row to the tables here and in [rules](rules.md#maps).
