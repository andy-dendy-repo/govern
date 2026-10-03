using System.Collections.Immutable;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

// The fairness checks on a built predefined map: it is the same after a 180° turn (terrain and rivers), every passable tile is
// reachable from every other without crossing a river that can't be crossed, and every spawn candidate leaves room for the
// starting units in the left half, as GameFactory places them (the k-th unit k columns towards the centre).
internal static class PredefinedMapBalanceValidator
{
    // Enough mismatches to locate a mistake without flooding the report.
    private const int MaxReported = 5;

    public static void Validate(Ruleset ruleset, GameMap map, ImmutableArray<HexCoord> spawns, List<RulesetProblem> problems)
    {
        CheckTerrainSymmetry(map, problems);
        CheckRiverSymmetry(map, problems);
        CheckConnected(ruleset, map, problems);
        CheckSpawns(ruleset, map, spawns, problems);
    }

    private static void CheckTerrainSymmetry(GameMap map, List<RulesetProblem> problems)
    {
        var mismatches = map.Tiles
            .Where(tile => Index(map, tile.Coord) < Index(map, map.MirrorOf(tile.Coord)))
            .Where(tile => tile.Terrain != map.TileAt(map.MirrorOf(tile.Coord)).Terrain)
            .Take(MaxReported);
        foreach (var tile in mismatches)
        {
            var mirror = map.MirrorOf(tile.Coord);
            problems.Add(new("$.rows", $"Tile {Name(tile.Coord)} is {tile.Terrain} but its mirror {Name(mirror)} is {map.TileAt(mirror).Terrain}."));
        }
    }

    // Every edge once, by its owner and side, compared with its mirror edge; a mismatched pair is reported from its first edge.
    private static void CheckRiverSymmetry(GameMap map, List<RulesetProblem> problems)
    {
        var mismatches = map.Tiles
            .SelectMany(static tile => Enum.GetValues<HexSide>().Select(side => new HexEdge(tile.Coord, side)))
            .Where(edge => map.Contains(edge.Neighbor()))
            .Select(edge => (Edge: edge, Mirror: HexEdge.Between(map.MirrorOf(edge.Owner), map.MirrorOf(edge.Neighbor()))!.Value))
            .Where(pair => Key(map, pair.Edge) < Key(map, pair.Mirror))
            .Where(pair => map.RiverBetween(pair.Edge.Owner, pair.Edge.Neighbor()) != map.RiverBetween(pair.Mirror.Owner, pair.Mirror.Neighbor()))
            .Take(MaxReported);
        foreach (var (edge, mirror) in mismatches)
        {
            var river = map.RiverBetween(edge.Owner, edge.Neighbor());
            var other = map.RiverBetween(mirror.Owner, mirror.Neighbor());
            problems.Add(new("$.rivers", $"The edge {Name(edge.Owner)}–{Name(edge.Neighbor())} has {river?.Id ?? "no river"} but its mirror {Name(mirror.Owner)}–{Name(mirror.Neighbor())} has {other?.Id ?? "no river"}."));
        }
    }

    // A flood fill from one passable tile over the steps a land unit can take.
    private static void CheckConnected(Ruleset ruleset, GameMap map, List<RulesetProblem> problems)
    {
        var passable = map.Tiles.Where(tile => ruleset.Terrain(tile.Terrain).IsPassable).Select(static tile => tile.Coord).ToList();
        if (passable.Count == 0)
        {
            problems.Add(new("$.rows", "The map has no passable tile."));
            return;
        }

        var reached = new HashSet<HexCoord> { passable[0] };
        var pending = new Stack<HexCoord>(reached);
        while (pending.TryPop(out var coord))
        {
            foreach (var next in coord.Neighbors())
            {
                if (reached.Contains(next) || !map.IsPassable(ruleset, next))
                {
                    continue;
                }

                if (map.RiverBetween(coord, next) is { } river && !ruleset.River(river).IsCrossable)
                {
                    continue;
                }

                reached.Add(next);
                pending.Push(next);
            }
        }

        if (passable.FirstOrDefault(coord => !reached.Contains(coord)) is var cut && reached.Count != passable.Count)
        {
            problems.Add(new("$.rows", $"Tile {Name(cut)} can't be reached from tile {Name(passable[0])}: lakes, mountains or rivers that can't be crossed cut the map."));
        }
    }

    private static void CheckSpawns(Ruleset ruleset, GameMap map, ImmutableArray<HexCoord> spawns, List<RulesetProblem> problems)
    {
        for (var index = 0; index < spawns.Length; index++)
        {
            for (var slot = 0; slot < ruleset.Setup.StartingUnits.Length; slot++)
            {
                var tile = HexCoord.FromOffset(spawns[index].Column + slot, spawns[index].Row);
                if (!map.Contains(tile) || tile.Column >= map.Width / 2 || !map.IsPassable(ruleset, tile))
                {
                    problems.Add(new($"$.spawns[{index}]", $"Starting unit {slot + 1} would stand on {Name(tile)}, which must be a passable tile in the left half."));
                }
            }
        }
    }

    private static int Index(GameMap map, HexCoord coord) => (coord.Row * map.Width) + coord.Column;

    private static int Key(GameMap map, HexEdge edge) => (Index(map, edge.Owner) * 3) + (int)edge.Side;

    private static string Name(HexCoord coord) => $"[{coord.Column}, {coord.Row}]";
}
