---
title: Predefined maps (the map pool)
tags: [game, maps, map-pool, predefined-map, basin, lobby, map-choice, spawns, mirror, symmetry, rivers, terrain, validation, authoring]
code: [maps, src/Govern.Core/Map/PredefinedMap.cs, src/Govern.Core/Map/PredefinedMapParser.cs, src/Govern.Core/Map/PredefinedMapValidator.cs, src/Govern.Core/Map/PredefinedMapBalanceValidator.cs, src/Govern.Application/Games/Services/MapPoolService.cs, src/Govern.Client/Features/Games/Services/MapPoolService.cs, src/Govern.Domain/ValueObjects/MapChoice.cs, src/Govern.Client/Features/Design/Pages/MapPreviewPage.razor]
updated: 2026-10-03
---

# Predefined maps (the map pool)

G18 ([plan](../plans/done/G18-predefined-maps.md)). Besides a generated map ([game logic](game-logic.md#map-generation)), a game can be played on a hand-made map from the **map pool**: every file `maps/<key>.json` at the repository root. The game's creator picks the map in the lobby ([games feature](../features/games.md#choosing-the-map-mapchoice)); the player rules are in [rules](rules.md#maps).

## Maps in the pool

| Key | Name | Size | Spawn candidates a side | Idea |
|---|---|---|---|---|
| `basin` | The Basin | 32 × 20 | 3 | Home valleys at both edges with a river (river near the edge, stream near the ridge), forests and hills; a mountain ridge with two hill passes in front of each valley; a 6 × 4 lake in the centre that splits the front into a north and a south flank, each with a stream, forests, hills and a desert corner. Ideas taken from Civ 6's Inland Sea (a big impassable centre) and Highlands (passes held from high ground), fitted to Govern: land units only, impassable lakes and mountains, rivers on edges |

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
  "spawns": [[1, 6], [2, 9], [2, 14]]
}
```

| Field | Meaning |
|---|---|
| `key` | Lower snake_case id; the file must be named `<key>.json`. `generated` and `random` are reserved (they are lobby choices) |
| `name` | What players see, 1 to 40 characters |
| `width`, `height` | Within the ruleset's `map.minSize`–`map.maxSize`; the height is even |
| `legend` | One character per terrain of the ruleset (`terrains.*.id`) |
| `rows` | `height` strings of `width` legend characters, top row first; odd rows are shifted half a tile right (odd-r layout, the same as the board) |
| `rivers` | Each river edge once: the tile that owns it as `[column, row]`, its `side` (`east`, `northEast` or `northWest`, the same three sides `TileRivers` stores) and the ruleset river (`rivers.*.id`) |
| `spawns` | The first player's spawn candidates as `[column, row]`; the second player's are their mirrors |

The parser is as strict as the ruleset's: camelCase names only, every field required, no unknown fields, no duplicate keys.

## Map validation

`PredefinedMap.Parse(ruleset, json)` reports every problem with its JSON path (`$.rows[3]`, `$.rivers[2].river`, `$.spawns[0]`); `ParseValid` throws `FormatException` listing them. The checks:

| Check | Why |
|---|---|
| Size within the ruleset's limits, even height | The mirror (`GameMap.MirrorOf`) keeps neighbours neighbours only on an even height |
| Every row the right length, every character in the legend, every legend terrain in the ruleset | The map is built from them |
| Every river edge on the map (the tile across it too), once, with a ruleset river | `TileRivers` stores an edge once |
| Every tile and every river edge equals its mirror | Neither player gets the better half, as on a generated map |
| Every passable tile reachable from every other without crossing a river that can't be crossed | Nobody is walled in |
| Every spawn candidate in the left half, with the row of starting units (the k-th unit k columns towards the centre) on passable tiles | `GameFactory` places the units that way |

`PredefinedMapTests.Parse_EveryPoolMap_IsValidForTheDefaultRuleset` validates every file in `maps/` against the default ruleset.

## How a game uses a pool map

- `GameSetup.Map` carries the map into `GameFactory.Create`: the game's state gets the map's tiles and rivers as they are (no generation), and the seed picks a spawn candidate for the first player and, on its own, one whose mirror the second player gets.
- The state keeps its own copy of the map, so editing a map file never changes a running game; pool maps need no versioning (unlike rulesets).
- The Api (`Application/Games/Services/MapPoolService`) and the client (`Client/Features/Games/Services/MapPoolService`) both embed `maps/*.json` and parse them once against the default ruleset, so the lobby's picker offers exactly the maps the Api accepts. An invalid file stops the first request that needs the pool.
- A fixed map can be learned after a few games: fog of war still hides units and cities, and the random spawn candidates vary the openings. More maps in the pool are the real answer.

## Adding a map

1. Write `maps/<key>.json`. Writing one half and mirroring it with a small script is the easy way (The Basin was made that way): a tile `[column, row]` mirrors to `[width − 1 − column, height − 1 − row]`, and a river edge mirrors to the edge between the mirrors of its two tiles.
2. Run `dotnet test --project tests/Govern.UnitTests` (the pool test reports every problem).
3. Look at it on the map preview, `/design/maps` (Development only): every tile in sight, with a starting unit on each spawn candidate and its mirror.
4. Add a row to the tables here and in [rules](rules.md#maps).
