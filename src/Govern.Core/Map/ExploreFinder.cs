namespace Govern.Core.Map;

/// <summary>
/// Where an exploring unit heads next: the nearest tile, by total movement cost, that is next to a tile its owner never saw.
/// Dijkstra over the hex grid as in <see cref="PathFinder"/> (same step costs, same tie-break by row, then column), stopping at the
/// first such tile. The unit's own tile never counts: its sight already covers its neighbours.
/// </summary>
internal static class ExploreFinder
{
    // The nearest tile next to an unexplored one, or null when no known path leads to any.
    public static HexCoord? Nearest(
        HexCoord origin,
        int fullMovement,
        Func<HexCoord, HexCoord, int?> stepCost,
        Func<HexCoord, bool> isFrontier)
    {
        var costs = new Dictionary<HexCoord, int> { [origin] = 0 };
        var frontier = new PriorityQueue<HexCoord, (int Cost, int Row, int Column)>();
        frontier.Enqueue(origin, (0, origin.Row, origin.Column));

        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > costs[current])
            {
                continue;
            }

            if (current != origin && isFrontier(current))
            {
                return current;
            }

            foreach (var next in current.Neighbors())
            {
                if (stepCost(current, next) is not { } step || step > fullMovement)
                {
                    continue;
                }

                var nextCost = priority.Cost + step;
                if (costs.TryGetValue(next, out var known) && known <= nextCost)
                {
                    continue;
                }

                costs[next] = nextCost;
                frontier.Enqueue(next, (nextCost, next.Row, next.Column));
            }
        }

        return null;
    }
}
