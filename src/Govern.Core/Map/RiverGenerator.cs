using Govern.Core.Engine;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// Lays down a new map's rivers from the ruleset's <c>map.generation.rivers</c> section, after the terrain. A river runs along
/// tile edges: it starts at a corner of a source tile (hills, mountains), each next edge shares a corner with the one before and
/// leads away from the source, and it ends at the first corner that touches a mouth tile (a lake) or the map's border. It gets
/// deeper downstream (the stages). Every edge is written together with its mirror (<see cref="GameMap.MirrorOf"/>), so both
/// halves of the map are the same; an edge whose mirror is not an edge, or that is already taken, isn't used. A stretch of a
/// kind that can't be crossed is laid as the last crossable kind before it wherever it would cut the passable tiles in two.
/// </summary>
internal static class RiverGenerator
{
    // Attempts per wanted river; a crowded or river-poor map just ends up with fewer rivers.
    private const int AttemptsPerRiver = 10;

    // Axial neighbour offsets in HexCoord.Neighbors order (east, then counter-clockwise).
    private static readonly (int Q, int R)[] Directions = [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)];

    public static GameMap Generate(Ruleset ruleset, GameMap map, DeterministicRandom random)
    {
        var rules = ruleset.Map.Generation.Rivers;
        var sources = map.Tiles.Where(tile => rules.Sources.Contains(tile.Terrain)).Select(static tile => tile.Coord).ToList();
        if (rules.Count == 0 || sources.Count == 0)
        {
            return map;
        }

        var layout = new Layout(ruleset, map);
        var placed = 0;
        for (var attempt = 0; placed < rules.Count && attempt < rules.Count * AttemptsPerRiver; attempt++)
        {
            if (Walk(layout, rules, sources[random.NextHashed(0, sources.Count)], random) is { } edges)
            {
                layout.Lay(rules, edges);
                placed++;
            }
        }

        return layout.Map;
    }

    // One river's edges from the source to its mouth, or null when it can't reach a mouth within the length limits.
    private static List<HexEdge>? Walk(Layout layout, RiverGenerationRules rules, HexCoord source, DeterministicRandom random)
    {
        var direction = random.NextHashed(0, Directions.Length);
        var a = source;
        var b = Step(source, direction);
        // The corner the river flows towards: one of the two tiles next to both a and b.
        var c = Step(source, (direction + (random.NextHashed(0, 2) == 0 ? 1 : 5)) % Directions.Length);

        var edges = new List<HexEdge>();
        var taken = new HashSet<HexEdge>();
        if (!layout.TryTake(a, b, taken, edges))
        {
            return null;
        }

        while (!layout.IsMouth(a, b, c))
        {
            if (edges.Count >= rules.MaxLength)
            {
                return null;
            }

            // The next edge shares the corner (a, b, c): it is a–c or b–c. Only edges that don't lead back towards the source.
            var distance = Distance(source, a, b, c);
            List<(HexCoord Kept, HexCoord Next)> candidates = [];
            foreach (var (kept, dropped) in new[] { (a, b), (b, a) })
            {
                var next = OtherCommonNeighbor(kept, c, dropped);
                if (Distance(source, kept, c, next) >= distance && layout.CanTake(kept, c, taken))
                {
                    candidates.Add((kept, next));
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var (keep, ahead) = candidates[random.NextHashed(0, candidates.Count)];
            layout.TryTake(keep, c, taken, edges);
            (a, b, c) = (keep, c, ahead);
        }

        return edges.Count >= rules.MinLength ? edges : null;
    }

    private static HexCoord Step(HexCoord coord, int direction) =>
        new(coord.Q + Directions[direction].Q, coord.R + Directions[direction].R);

    // Of the two tiles next to both x and y, the one that isn't `not`.
    private static HexCoord OtherCommonNeighbor(HexCoord x, HexCoord y, HexCoord not)
    {
        var direction = Array.IndexOf(Directions, (y.Q - x.Q, y.R - x.R));
        var first = Step(x, (direction + 1) % Directions.Length);
        return first == not ? Step(x, (direction + 5) % Directions.Length) : first;
    }

    // How far a corner is from the source: the sum of its three tiles' distances.
    private static int Distance(HexCoord source, HexCoord a, HexCoord b, HexCoord c) =>
        source.DistanceTo(a) + source.DistanceTo(b) + source.DistanceTo(c);

    // The map being given rivers, with the checks every edge passes.
    private sealed class Layout(Ruleset ruleset, GameMap map)
    {
        private readonly HashSet<HexEdge> _rivers = [];
        private readonly HashSet<HexEdge> _blocked = [];

        public GameMap Map { get; private set; } = map;

        // A corner ends a river when one of its tiles is off the map or a mouth terrain.
        public bool IsMouth(HexCoord a, HexCoord b, HexCoord c) => new[] { a, b, c }.Any(coord =>
            !Map.Contains(coord) || ruleset.Map.Generation.Rivers.Mouths.Contains(Map.TileAt(coord).Terrain));

        // An edge can carry the river when both its tiles are on the map, one of them is passable (a river between two
        // impassable tiles changes nothing), its mirror is an edge too, and neither is taken by this river or another.
        public bool CanTake(HexCoord a, HexCoord b, HashSet<HexEdge> taken)
        {
            if (!Map.Contains(a) || !Map.Contains(b) || (!Map.IsPassable(ruleset, a) && !Map.IsPassable(ruleset, b)))
            {
                return false;
            }

            var edge = HexEdge.Between(a, b)!.Value;
            return HexEdge.Between(Map.MirrorOf(a), Map.MirrorOf(b)) is { } mirror
                && !taken.Contains(edge) && !taken.Contains(mirror)
                && !_rivers.Contains(edge) && !_rivers.Contains(mirror);
        }

        public bool TryTake(HexCoord a, HexCoord b, HashSet<HexEdge> taken, List<HexEdge> edges)
        {
            if (!CanTake(a, b, taken))
            {
                return false;
            }

            var edge = HexEdge.Between(a, b)!.Value;
            edges.Add(edge);
            taken.Add(edge);
            taken.Add(MirrorOf(edge));
            return true;
        }

        // Writes the river and its mirror, stage by stage.
        public void Lay(RiverGenerationRules rules, List<HexEdge> edges)
        {
            RiverType? lastCrossable = null;
            for (var index = 0; index < edges.Count; index++)
            {
                var river = rules.Stages.Last(stage => stage.FromSegment <= index).River;
                if (ruleset.River(river).IsCrossable)
                {
                    lastCrossable = river;
                }
                else if (!TryBlock(edges[index]))
                {
                    // The validator keeps the first stage crossable, so there always is one.
                    river = lastCrossable!.Value;
                }

                foreach (var edge in new[] { edges[index], MirrorOf(edges[index]) })
                {
                    _rivers.Add(edge);
                    Map = Map.WithRiver(edge, river);
                }
            }
        }

        private HexEdge MirrorOf(HexEdge edge) => HexEdge.Between(Map.MirrorOf(edge.Owner), Map.MirrorOf(edge.Neighbor()))!.Value;

        // Blocks the edge and its mirror when every passable tile stays reachable from every other.
        private bool TryBlock(HexEdge edge)
        {
            var mirror = MirrorOf(edge);
            _blocked.Add(edge);
            _blocked.Add(mirror);
            if (PassableTilesAreConnected())
            {
                return true;
            }

            _blocked.Remove(edge);
            _blocked.Remove(mirror);
            return false;
        }

        // A flood fill from one passable tile, never stepping over a blocked edge, reaches every passable tile.
        private bool PassableTilesAreConnected()
        {
            var passable = Map.Tiles.Where(tile => ruleset.Terrain(tile.Terrain).IsPassable).Select(static tile => tile.Coord).ToList();
            if (passable.Count == 0)
            {
                return true;
            }

            var reached = new HashSet<HexCoord> { passable[0] };
            var pending = new Stack<HexCoord>([passable[0]]);
            while (pending.TryPop(out var current))
            {
                foreach (var next in current.Neighbors())
                {
                    if (Map.IsPassable(ruleset, next) && !_blocked.Contains(HexEdge.Between(current, next)!.Value) && reached.Add(next))
                    {
                        pending.Push(next);
                    }
                }
            }

            return reached.Count == passable.Count;
        }
    }
}
