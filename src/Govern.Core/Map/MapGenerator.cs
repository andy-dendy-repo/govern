using Govern.Core.Engine;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// Lays down a new map's terrain from the ruleset's <c>map.generation</c> section. The map starts as the default terrain; each
/// listed terrain then grows in patches from random seeds until it covers its share. Every tile is written together with its
/// mirror (<see cref="GameMap.MirrorOf"/>), so both halves are the same and neither player gets the better half. Reserved
/// tiles (where units start) and their mirrors stay default terrain, and an impassable tile is only placed while every
/// passable tile stays reachable from every other.
/// </summary>
internal static class MapGenerator
{
    // The blank map must be covered with the default terrain.
    public static GameMap Generate(Ruleset ruleset, GameMap map, DeterministicRandom random, IReadOnlyCollection<HexCoord> reserved)
    {
        var layout = new Layout(ruleset, map, reserved);

        foreach (var patch in ruleset.Map.Generation.Patches)
        {
            LayPatches(layout, patch, random);
        }

        return map with { Tiles = [.. map.Tiles.Select(tile => tile with { Terrain = layout.Terrains[layout.IndexOf(tile.Coord)] })] };
    }

    // Seeds patches until the terrain covers its share; a bounded number of seeds, so a crowded map still finishes.
    private static void LayPatches(Layout layout, TerrainPatchRules patch, DeterministicRandom random)
    {
        var target = layout.Map.Tiles.Length * patch.SharePerMille / 1000;
        var placed = 0;
        for (var attempt = 0; placed < target && attempt < layout.Map.Tiles.Length; attempt++)
        {
            var seed = layout.Map.Tiles[random.NextHashed(0, layout.Map.Tiles.Length)].Coord;
            placed += GrowPatch(layout, patch, seed, target - placed, random);
        }
    }

    // Grows one patch over random neighbours of the tiles placed so far; returns the number of tiles placed (mirrors included).
    private static int GrowPatch(Layout layout, TerrainPatchRules patch, HexCoord seed, int remaining, DeterministicRandom random)
    {
        var placed = 0;
        var size = 0;
        List<HexCoord> frontier = [seed];
        while (size < patch.PatchSize && placed < remaining && frontier.Count > 0)
        {
            var index = random.NextHashed(0, frontier.Count);
            var coord = frontier[index];
            frontier[index] = frontier[^1];
            frontier.RemoveAt(frontier.Count - 1);

            var count = layout.TryPlace(coord, patch.Terrain);
            if (count == 0)
            {
                continue;
            }

            placed += count;
            size++;
            frontier.AddRange(coord.Neighbors().Where(layout.Map.Contains));
        }

        return placed;
    }

    // The terrain being laid down, by tile index, with the checks every placement passes.
    private sealed class Layout(Ruleset ruleset, GameMap map, IReadOnlyCollection<HexCoord> reserved)
    {
        private readonly HashSet<HexCoord> _reserved = [.. reserved.Where(map.Contains).SelectMany(coord => new[] { coord, map.MirrorOf(coord) })];

        private readonly bool[] _passable = [.. map.Tiles.Select(tile => ruleset.Terrain(tile.Terrain).IsPassable)];
        private readonly bool[] _reached = new bool[map.Tiles.Length];
        private readonly int[] _pending = new int[map.Tiles.Length];

        public GameMap Map => map;

        public TerrainType[] Terrains { get; } = [.. map.Tiles.Select(static tile => tile.Terrain)];

        public int IndexOf(HexCoord coord) => (coord.Row * map.Width) + coord.Column;

        // Writes the terrain on the tile and its mirror; returns how many tiles changed (0 when the placement is not allowed).
        public int TryPlace(HexCoord coord, TerrainType terrain)
        {
            var mirror = map.MirrorOf(coord);
            if (!IsFree(coord) || !IsFree(mirror))
            {
                return 0;
            }

            var passable = ruleset.Terrain(terrain).IsPassable;
            Set(coord, terrain, passable);
            Set(mirror, terrain, passable);
            if (!passable && !PassableTilesAreConnected())
            {
                Set(coord, ruleset.Map.DefaultTerrain, passable: true);
                Set(mirror, ruleset.Map.DefaultTerrain, passable: true);
                return 0;
            }

            return coord == mirror ? 1 : 2;
        }

        private bool IsFree(HexCoord coord) => !_reserved.Contains(coord) && Terrains[IndexOf(coord)] == ruleset.Map.DefaultTerrain;

        // The default terrain is validated as passable.
        private void Set(HexCoord coord, TerrainType terrain, bool passable)
        {
            Terrains[IndexOf(coord)] = terrain;
            _passable[IndexOf(coord)] = passable;
        }

        // A flood fill from one passable tile reaches every passable tile. It runs once per impassable placement, so it reuses
        // its buffers instead of allocating.
        private bool PassableTilesAreConnected()
        {
            var first = Array.IndexOf(_passable, true);
            if (first < 0)
            {
                return false;
            }

            Array.Clear(_reached);
            var top = 0;
            _pending[top++] = first;
            _reached[first] = true;
            var reached = 1;
            while (top > 0)
            {
                foreach (var next in map.Tiles[_pending[--top]].Coord.Neighbors())
                {
                    if (map.Contains(next) && IndexOf(next) is var index && !_reached[index] && _passable[index])
                    {
                        _reached[index] = true;
                        _pending[top++] = index;
                        reached++;
                    }
                }
            }

            return reached == _passable.Count(static passable => passable);
        }
    }
}
