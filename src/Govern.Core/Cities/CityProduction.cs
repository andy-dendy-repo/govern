using Govern.Core.Economy;
using Govern.Core.Rulesets;

namespace Govern.Core.Cities;

/// <summary>
/// A city's production per turn: what it adds to its own item, or to the one job it feeds in its territory.
/// </summary>
public static class CityProduction
{
    /// <summary>A city's production per turn.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's buildings.</param>
    /// <returns><see cref="CityRules.ProductionPerTurn"/> plus each building's <see cref="BuildingDefinition.Production"/>.</returns>
    public static int PerTurn(Ruleset ruleset, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        return ruleset.Cities.ProductionPerTurn + buildings.Sum(building => ruleset.Building(building).Production);
    }
}
