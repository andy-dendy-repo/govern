using System.Collections.Immutable;

namespace Govern.Core.Map;

/// <summary>
/// The cheapest path to a tile over any number of turns: Dijkstra over the hex grid by total movement cost, without the
/// one-turn bound of <see cref="ReachabilityCalculator"/>. A step that costs more than the unit's full movement can never be
/// taken (a unit never spends more than it has left), so it is skipped. Ties break by row, then column, as in reachability.
/// </summary>
internal static class PathFinder
{
    // Tiles entered in order with the cost of each step, ending with the target; null when no path leads there.
    public static ImmutableArray<(HexCoord Tile, int Cost)>? Find(
        HexCoord origin,
        HexCoord target,
        int fullMovement,
        Func<HexCoord, HexCoord, int?> stepCost)
    {
        var costs = new Dictionary<HexCoord, int> { [origin] = 0 };
        var previous = new Dictionary<HexCoord, (HexCoord From, int Step)>();
        var frontier = new PriorityQueue<HexCoord, (int Cost, int Row, int Column)>();
        frontier.Enqueue(origin, (0, origin.Row, origin.Column));

        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (current == target)
            {
                return Trace(origin, target, previous);
            }

            if (priority.Cost > costs[current])
            {
                continue;
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
                previous[next] = (current, step);
                frontier.Enqueue(next, (nextCost, next.Row, next.Column));
            }
        }

        return null;
    }

    // How many of the owner's coming turns a walk along the path still needs (0: it arrives now): the steps the movement left
    // pays for happen now, then each turn starts with full movement and walks while the next step fits, as the walk itself does.
    public static int Turns(ImmutableArray<(HexCoord Tile, int Cost)> path, int movementLeft, int fullMovement)
    {
        var turns = 0;
        var movement = movementLeft;
        foreach (var (_, cost) in path)
        {
            if (cost > movement)
            {
                turns++;
                movement = fullMovement;
            }

            movement -= cost;
        }

        return turns;
    }

    private static ImmutableArray<(HexCoord Tile, int Cost)> Trace(
        HexCoord origin,
        HexCoord target,
        Dictionary<HexCoord, (HexCoord From, int Step)> previous)
    {
        var path = new List<(HexCoord, int)>();
        for (var coord = target; coord != origin; coord = previous[coord].From)
        {
            path.Add((coord, previous[coord].Step));
        }

        path.Reverse();
        return [.. path];
    }
}
