using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Map;

/// <summary>
/// Dijkstra over the hex grid, bounded by the unit's remaining movement. Units never pass through other units or enemy cities.
/// </summary>
internal static class ReachabilityCalculator
{
    public static Reachability Compute(GameState state, Unit unit)
    {
        var costs = new Dictionary<HexCoord, int> { [unit.Position] = 0 };
        var previous = new Dictionary<HexCoord, HexCoord>();
        // Ties break by row, then column, so paths never depend on the heap implementation.
        var frontier = new PriorityQueue<HexCoord, (int Cost, int Row, int Column)>();
        frontier.Enqueue(unit.Position, (0, unit.Position.Row, unit.Position.Column));

        while (frontier.TryDequeue(out var current, out var priority))
        {
            var cost = priority.Cost;
            if (cost > costs[current])
            {
                continue;
            }

            foreach (var next in current.Neighbors().Where(coord => CanEnter(state, unit, coord)))
            {
                var nextCost = cost + TerrainCatalog.Get(state.Map.TileAt(next).Terrain).MovementCost;
                if (nextCost > unit.MovementLeft || (costs.TryGetValue(next, out var known) && known <= nextCost))
                {
                    continue;
                }

                costs[next] = nextCost;
                previous[next] = current;
                frontier.Enqueue(next, (nextCost, next.Row, next.Column));
            }
        }

        return new Reachability(unit.Position, costs, previous);
    }

    private static bool CanEnter(GameState state, Unit unit, HexCoord coord) =>
        state.Map.IsPassable(coord)
        && state.UnitAt(coord) is null
        && (state.CityAt(coord) is not { } city || city.Owner == unit.Owner);
}
