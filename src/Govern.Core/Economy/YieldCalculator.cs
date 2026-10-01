using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Rulesets;

namespace Govern.Core.Economy;

/// <summary>
/// What tiles give and how resource amounts add up. Used by the end of turn on the state and by the client on the view, so both
/// count the same way.
/// </summary>
public static class YieldCalculator
{
    /// <summary>
    /// What a tile gives every turn: its terrain's yields, plus, per resource, the most any river on its edges gives, plus its
    /// improvement's yields.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="terrain">The tile's terrain.</param>
    /// <param name="rivers">The rivers on the tile's edges (<see cref="GameMap.RiversAround"/>).</param>
    /// <param name="improvement">The tile's improvement, if any.</param>
    /// <returns>The amounts, in the ruleset's order, without zeros.</returns>
    public static ImmutableArray<ResourceAmount> TileYields(Ruleset ruleset, TerrainType terrain, IEnumerable<RiverType> rivers, ImprovementType? improvement)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(rivers);

        var riverYields = rivers
            .Distinct()
            .SelectMany(river => ruleset.River(river).Yields)
            .GroupBy(static amount => amount.Resource)
            .Select(static group => new ResourceAmount(group.Key, group.Max(static amount => amount.Amount)));
        var improvementYields = improvement is { } built ? ruleset.Improvement(built).Yields : [];
        return Sum(ruleset, ruleset.Terrain(terrain).Yields.Concat(riverYields).Concat(improvementYields));
    }

    /// <summary>Adds amounts up per resource.</summary>
    /// <param name="ruleset">The game's ruleset (its resource order).</param>
    /// <param name="amounts">Amounts, possibly of the same resource several times; negative amounts subtract.</param>
    /// <returns>One amount per resource, in the ruleset's order, without zeros.</returns>
    public static ImmutableArray<ResourceAmount> Sum(Ruleset ruleset, IEnumerable<ResourceAmount> amounts)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(amounts);

        var totals = new Dictionary<ResourceType, int>();
        foreach (var amount in amounts)
        {
            totals[amount.Resource] = totals.GetValueOrDefault(amount.Resource) + amount.Amount;
        }

        // In the ruleset's order, so a stock (and the view's fingerprint) never depends on dictionary order.
        return [.. ruleset.Resources
            .Where(resource => totals.GetValueOrDefault(resource.Id) != 0)
            .Select(resource => new ResourceAmount(resource.Id, totals[resource.Id]))];
    }

    /// <summary>What a city adds to its stock every turn.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="tileYields">The yields of every tile in the city's territory (<see cref="TileYields"/>), its own tile included.</param>
    /// <param name="buildings">The city's buildings.</param>
    /// <returns>The tiles' yields, plus <see cref="CityRules.Yields"/> and each building's yields, in the ruleset's order.</returns>
    public static ImmutableArray<ResourceAmount> CityIncome(Ruleset ruleset, IEnumerable<ResourceAmount> tileYields, IEnumerable<BuildingType> buildings)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(tileYields);
        ArgumentNullException.ThrowIfNull(buildings);
        var buildingYields = buildings.SelectMany(building => ruleset.Building(building).Yields);
        return Sum(ruleset, tileYields.Concat(ruleset.Cities.Yields).Concat(buildingYields));
    }

    /// <summary>How much of one resource a list holds.</summary>
    /// <param name="amounts">The amounts, such as a city's stock.</param>
    /// <param name="resource">The resource.</param>
    /// <returns>Its amount, or 0 when it is not listed.</returns>
    public static int AmountOf(IEnumerable<ResourceAmount> amounts, ResourceType resource)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        return amounts.Where(amount => amount.Resource == resource).Sum(static amount => amount.Amount);
    }
}
