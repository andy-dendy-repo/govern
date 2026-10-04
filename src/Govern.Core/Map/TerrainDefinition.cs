using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Units;

namespace Govern.Core.Map;

/// <summary>
/// Rules data for one <see cref="TerrainType"/>, from the game's ruleset.
/// </summary>
/// <param name="Id">The terrain this definition describes.</param>
/// <param name="MovementCost">Movement points a unit spends to enter the tile (at least 1).</param>
/// <param name="IsPassable">Whether land units may enter the tile at all.</param>
/// <param name="SightBonus">Extra sight, in tiles, for a unit or city standing on the tile (0 for none).</param>
/// <param name="DefenceBonus">Extra strength for a unit defending on the tile (0 for none).</param>
/// <param name="Yields">What the tile gives every turn on its own, before rivers and improvements (may be empty).</param>
/// <param name="IsWater">Whether naval units may sail the tile (land units then can't enter it: it is never passable).</param>
public sealed record TerrainDefinition(
    TerrainType Id,
    int MovementCost,
    bool IsPassable,
    int SightBonus,
    int DefenceBonus,
    ImmutableArray<ResourceAmount> Yields,
    bool IsWater = false)
{
    /// <summary>Whether a unit of a domain may enter a tile of this terrain: land units passable land, naval units water.</summary>
    /// <param name="domain">The unit's domain.</param>
    /// <returns><see langword="true"/> when the unit may stand on the tile.</returns>
    public bool AllowsDomain(UnitDomain domain) => domain == UnitDomain.Naval ? IsWater : IsPassable;
}
