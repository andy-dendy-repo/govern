using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Map;

/// <summary>
/// Dijkstra over the hex grid, bounded by the unit's remaining movement. Units never pass through other units or enemy cities.
/// A step costs the entered tile's movement cost plus the crossing cost of a river on the edge between the two tiles, and a
/// river that can't be crossed blocks the step. A bridged edge is crossed as if it had no river.
/// </summary>
internal static class ReachabilityCalculator
{
    public static Reachability Compute(Ruleset ruleset, GameState state, Unit unit) =>
        Compute(unit, (from, to) => StepCost(ruleset, state, unit, from, to));

    // The cost of the unit's step between neighbours on the full state; null when it can't take it (impassable, a unit there, an
    // enemy city, or a river it can't cross).
    public static int? StepCost(Ruleset ruleset, GameState state, Unit unit, HexCoord from, HexCoord to) =>
        state.Map.IsPassable(ruleset, to)
        && state.UnitAt(to) is null
        && (state.CityAt(to) is not { } city || city.Owner == unit.Owner)
            ? StepCost(ruleset, state.Map.TileAt(to).Terrain, state.Map.CrossedRiverBetween(from, to))
            : null;

    // The cost of stepping onto a passable tile with the given terrain over an edge with the given river; null when the
    // river can't be crossed. Shared with the view previews, so both price a step the same way.
    public static int? StepCost(Ruleset ruleset, TerrainType terrain, RiverType? river)
    {
        var cost = ruleset.Terrain(terrain).MovementCost;
        if (river is not { } type)
        {
            return cost;
        }

        var definition = ruleset.River(type);
        return definition.IsCrossable ? cost + definition.CrossingCost : null;
    }

    // The search itself; the callers price each step between neighbours (null when the unit can't take it).
    public static Reachability Compute(Unit unit, Func<HexCoord, HexCoord, int?> stepCost)
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

            foreach (var next in current.Neighbors())
            {
                if (stepCost(current, next) is not { } step)
                {
                    continue;
                }

                var nextCost = cost + step;
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
