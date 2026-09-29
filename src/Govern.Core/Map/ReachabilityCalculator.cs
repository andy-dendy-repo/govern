using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Map;

/// <summary>
/// Dijkstra over the hex grid, bounded by the unit's remaining movement. Units never pass through other units or enemy cities.
/// </summary>
internal static class ReachabilityCalculator
{
    public static Reachability Compute(GameState state, Unit unit) => Compute(
        unit,
        coord => state.Map.IsPassable(coord)
            && state.UnitAt(coord) is null
            && (state.CityAt(coord) is not { } city || city.Owner == unit.Owner),
        coord => TerrainCatalog.Get(state.Map.TileAt(coord).Terrain).MovementCost);

    // The search itself; the callers say which tiles the unit may enter and what each costs.
    public static Reachability Compute(Unit unit, Func<HexCoord, bool> canEnter, Func<HexCoord, int> costOf)
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

            foreach (var next in current.Neighbors().Where(canEnter))
            {
                var nextCost = cost + costOf(next);
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
}
