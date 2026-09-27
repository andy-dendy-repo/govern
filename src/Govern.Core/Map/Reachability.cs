using System.Collections.Immutable;

namespace Govern.Core.Map;

/// <summary>
/// Cheapest known cost and path to every tile a unit can reach this turn.
/// </summary>
internal sealed class Reachability(HexCoord origin, IReadOnlyDictionary<HexCoord, int> costs, IReadOnlyDictionary<HexCoord, HexCoord> previous)
{
    public IEnumerable<HexCoord> Destinations => costs.Keys.Where(coord => coord != origin);

    public bool CanReach(HexCoord target) => target != origin && costs.ContainsKey(target);

    public int CostTo(HexCoord target) => costs[target];

    // Tiles entered in order, excluding the origin and ending with the target.
    public ImmutableArray<HexCoord> PathTo(HexCoord target)
    {
        var path = new List<HexCoord>();
        for (var coord = target; coord != origin; coord = previous[coord])
        {
            path.Add(coord);
        }

        path.Reverse();
        return [.. path];
    }
}
