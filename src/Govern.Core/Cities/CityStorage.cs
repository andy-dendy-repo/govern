using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Rulesets;

namespace Govern.Core.Cities;

/// <summary>
/// A city's storage: how much of each stocked resource it can hold. Every city has <see cref="CityRules.Storage"/>, and each of
/// its working buildings adds its <see cref="BuildingDefinition.Storage"/>. At the end of every turn a city's stock is cut to
/// its caps; what doesn't fit is lost.
/// </summary>
public static class CityStorage
{
    /// <summary>A city's storage caps.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's working buildings (<see cref="City.WorkingBuildings"/>).</param>
    /// <returns>One cap per stocked resource, in the ruleset's order: the default plus what the buildings add.</returns>
    public static ImmutableArray<ResourceAmount> Caps(Ruleset ruleset, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        return YieldCalculator.Sum(ruleset, ruleset.Cities.Storage.Concat(buildings.SelectMany(building => ruleset.Building(building).Storage)));
    }

    /// <summary>A stock cut to its caps.</summary>
    /// <param name="ruleset">The game's ruleset (its resource order).</param>
    /// <param name="stock">The stock.</param>
    /// <param name="caps">The caps (<see cref="Caps"/>); a resource without one is never cut.</param>
    /// <returns>The stock with every amount at most its cap, in the ruleset's order, without zeros.</returns>
    public static ImmutableArray<ResourceAmount> Cut(Ruleset ruleset, IEnumerable<ResourceAmount> stock, ImmutableArray<ResourceAmount> caps)
    {
        ArgumentNullException.ThrowIfNull(stock);
        return YieldCalculator.Sum(ruleset, stock.Select(amount => amount with { Amount = Math.Min(amount.Amount, CapOf(caps, amount.Resource)) }));
    }

    /// <summary>What a stock holds over its caps, which is lost when it is cut.</summary>
    /// <param name="ruleset">The game's ruleset (its resource order).</param>
    /// <param name="stock">The stock.</param>
    /// <param name="caps">The caps (<see cref="Caps"/>).</param>
    /// <returns>The excess per resource, in the ruleset's order; empty when everything fits.</returns>
    public static ImmutableArray<ResourceAmount> Excess(Ruleset ruleset, IEnumerable<ResourceAmount> stock, ImmutableArray<ResourceAmount> caps)
    {
        ArgumentNullException.ThrowIfNull(stock);
        return YieldCalculator.Sum(ruleset, stock.Select(amount => amount with { Amount = Math.Max(0, amount.Amount - CapOf(caps, amount.Resource)) }));
    }

    /// <summary>How much of one resource a city can hold.</summary>
    /// <param name="caps">The city's caps (<see cref="Caps"/>).</param>
    /// <param name="resource">The resource.</param>
    /// <returns>Its cap, or <see cref="int.MaxValue"/> when it has none (science is never stocked).</returns>
    public static int CapOf(ImmutableArray<ResourceAmount> caps, ResourceType resource) =>
        caps.FirstOrDefault(cap => cap.Resource == resource)?.Amount ?? int.MaxValue;
}
