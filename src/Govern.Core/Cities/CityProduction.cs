using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Cities;

/// <summary>
/// A city's production per turn: what it adds to its own item, or to the one job it feeds in its territory.
/// </summary>
public static class CityProduction
{
    /// <summary>A city's production per turn.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's working buildings (<see cref="City.WorkingBuildings"/>).</param>
    /// <returns><see cref="CityRules.ProductionPerTurn"/> plus each building's <see cref="BuildingDefinition.Production"/>.</returns>
    public static int PerTurn(Ruleset ruleset, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        return Sources(ruleset, buildings).Sum(static source => source.Amount);
    }

    /// <summary>Where a city's production per turn comes from.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's working buildings (<see cref="City.WorkingBuildings"/>).</param>
    /// <returns>
    /// The city's own <see cref="CityRules.ProductionPerTurn"/> first, then each building that adds production, in the order given.
    /// </returns>
    public static ImmutableArray<ProductionSource> Sources(Ruleset ruleset, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        return
        [
            new ProductionSource(null, ruleset.Cities.ProductionPerTurn),
            .. buildings.Where(building => ruleset.Building(building).Production > 0)
                .Select(building => new ProductionSource(building, ruleset.Building(building).Production)),
        ];
    }

    /// <summary>The resources a city pays when it begins producing an item.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="building">The building it produces, if any (it comes before the unit).</param>
    /// <param name="unit">The unit it produces otherwise.</param>
    /// <returns>The item's resource cost (may be empty).</returns>
    public static ImmutableArray<ResourceAmount> ResourceCost(Ruleset ruleset, BuildingType? building, UnitType unit)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return building is { } produced ? ruleset.Building(produced).ResourceCost : ruleset.Unit(unit).ResourceCost;
    }

    /// <summary>A city leaving its item before it is done: the resource cost it paid goes back to its stock.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="city">The city.</param>
    /// <returns>The city with the cost back in its stock and nothing paid; the city itself when nothing was paid.</returns>
    public static City Refund(Ruleset ruleset, City city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (!city.ProductionPaid)
        {
            return city;
        }

        var stock = YieldCalculator.Sum(ruleset, city.StockList.Concat(ResourceCost(ruleset, city.ProducingBuilding, city.Producing)));
        return city with { Stock = stock.IsEmpty ? null : stock, ProductionPaid = false };
    }
}
