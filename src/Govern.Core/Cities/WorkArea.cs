using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;

namespace Govern.Core.Cities;

/// <summary>
/// A city's work area: the tiles around it, up to <see cref="CityRules.WorkRadius"/> away, where builders build improvements
/// with the city's production and whose improvements yield to the city's owner. The city's own tile is not part of it. Two work
/// areas never overlap (the ruleset keeps the city spacing above twice the radius).
/// </summary>
public static class WorkArea
{
    /// <summary>Whether a tile is in a city's work area.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="city">The city's tile.</param>
    /// <param name="tile">The tile.</param>
    /// <returns><see langword="true"/> when the tile is 1 to <see cref="CityRules.WorkRadius"/> tiles from the city.</returns>
    public static bool Contains(Ruleset ruleset, HexCoord city, HexCoord tile)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var distance = city.DistanceTo(tile);
        return distance >= 1 && distance <= ruleset.Cities.WorkRadius;
    }

    /// <summary>Whether a tile is the city's own tile or in its work area: the ground a city's builders use its production on.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="city">The city's tile.</param>
    /// <param name="tile">The tile.</param>
    /// <returns><see langword="true"/> when the tile is at most <see cref="CityRules.WorkRadius"/> tiles from the city.</returns>
    public static bool Covers(Ruleset ruleset, HexCoord city, HexCoord tile)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return city.DistanceTo(tile) <= ruleset.Cities.WorkRadius;
    }

    /// <summary>The production a city makes per turn: the ruleset's base plus its buildings' extra production.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's buildings.</param>
    /// <returns>Production points per turn.</returns>
    public static int ProductionPerTurn(Ruleset ruleset, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        return ruleset.Cities.ProductionPerTurn + buildings.Sum(building => ruleset.Building(building).Production);
    }
}
