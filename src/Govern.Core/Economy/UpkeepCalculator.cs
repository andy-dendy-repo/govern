using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Economy;

/// <summary>
/// How a city pays the upkeep of what stands on its territory, and what a stock or the land lacks. Used by the end of turn on
/// the state and by the client on the view, so both decide the same way.
/// </summary>
public static class UpkeepCalculator
{
    /// <summary>
    /// Pays a city's upkeep from its stock, item by item in the order of <paramref name="priority"/>: buildings in the ruleset's
    /// order, units lowest health first (then by id). An item is paid in full or not at all, and an unpaid item takes nothing,
    /// so a later, cheaper item can still be paid.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="stock">The city's stock.</param>
    /// <param name="buildings">The city's buildings.</param>
    /// <param name="units">The city owner's units on the city's territory.</param>
    /// <param name="priority">Whether buildings or units are paid first.</param>
    /// <returns>The stock after paying, what was paid, and what wasn't.</returns>
    public static UpkeepResult Pay(
        Ruleset ruleset,
        IEnumerable<ResourceAmount> stock,
        IEnumerable<BuildingType> buildings,
        IEnumerable<UpkeepUnit> units,
        UpkeepPriority priority)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(units);

        var owned = buildings.ToHashSet();
        var buildingItems = ruleset.Buildings
            .Where(building => owned.Contains(building.Id))
            .Select(static building => (Building: (BuildingType?)building.Id, Unit: (UnitId?)null, building.Upkeep));
        var unitItems = units
            .OrderByDescending(static unit => unit.Damage)
            .ThenBy(static unit => unit.Id.Value)
            .Select(unit => (Building: (BuildingType?)null, Unit: (UnitId?)unit.Id, ruleset.Unit(unit.Type).Upkeep));
        var items = priority == UpkeepPriority.UnitsFirst ? unitItems.Concat(buildingItems) : buildingItems.Concat(unitItems);

        var left = Sum(ruleset, stock);
        var paid = new List<ResourceAmount>();
        var unpaidBuildings = ImmutableArray.CreateBuilder<BuildingType>();
        var unpaidUnits = ImmutableArray.CreateBuilder<UnitId>();
        foreach (var (building, unit, upkeep) in items)
        {
            if (Missing(ruleset, left, upkeep).IsEmpty)
            {
                left = Subtract(ruleset, left, upkeep);
                paid.AddRange(upkeep);
            }
            else if (building is { } unpaidBuilding)
            {
                unpaidBuildings.Add(unpaidBuilding);
            }
            else
            {
                unpaidUnits.Add(unit!.Value);
            }
        }

        return new UpkeepResult(left, Sum(ruleset, paid), unpaidBuildings.ToImmutable(), unpaidUnits.ToImmutable());
    }

    /// <summary>What <paramref name="held"/> lacks to cover <paramref name="needed"/>.</summary>
    /// <param name="ruleset">The game's ruleset (its resource order).</param>
    /// <param name="held">What there is, such as a stock or the land's yields.</param>
    /// <param name="needed">What is needed, such as a cost or an upkeep.</param>
    /// <returns>The missing amount of each resource, in the ruleset's order; empty when everything is covered.</returns>
    public static ImmutableArray<ResourceAmount> Missing(Ruleset ruleset, IEnumerable<ResourceAmount> held, IEnumerable<ResourceAmount> needed)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(needed);
        var have = held.ToList();
        var shortfall = Sum(ruleset, needed)
            .Select(amount => amount with { Amount = amount.Amount - YieldCalculator.AmountOf(have, amount.Resource) })
            .Where(static amount => amount.Amount > 0);
        return Sum(ruleset, shortfall);
    }

    /// <summary>Takes a cost out of a stock that covers it (<see cref="Missing"/> is empty).</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="stock">The stock.</param>
    /// <param name="cost">The cost.</param>
    /// <returns>The stock left, in the ruleset's order, without zeros.</returns>
    public static ImmutableArray<ResourceAmount> Subtract(Ruleset ruleset, IEnumerable<ResourceAmount> stock, IEnumerable<ResourceAmount> cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        return Sum(ruleset, stock.Concat(cost.Select(static amount => amount with { Amount = -amount.Amount })));
    }

    /// <summary>
    /// Each building's damage after a turn's upkeep: an unpaid one takes <see cref="UpkeepRules.ShortageDamage"/> up to
    /// <see cref="UpkeepRules.BuildingHealth"/>; a paid one recovers <see cref="UpkeepRules.BuildingRecovery"/> down to 0.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="buildings">The city's buildings.</param>
    /// <param name="damageOf">Each building's damage before the upkeep.</param>
    /// <param name="unpaid">The buildings whose upkeep wasn't paid (<see cref="UpkeepResult.UnpaidBuildings"/>).</param>
    /// <returns>The hurt buildings with their new damage, in the order given; an unhurt building is left out.</returns>
    public static ImmutableArray<BuildingDamage> DamageAfter(
        Ruleset ruleset,
        IEnumerable<BuildingType> buildings,
        Func<BuildingType, int> damageOf,
        IEnumerable<BuildingType> unpaid)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(damageOf);
        var rules = ruleset.Upkeep;
        var unpaidSet = unpaid.ToHashSet();
        return [.. buildings
            .Select(building => new BuildingDamage(building, unpaidSet.Contains(building)
                ? Math.Min(rules.BuildingHealth, damageOf(building) + rules.ShortageDamage)
                : Math.Max(0, damageOf(building) - rules.BuildingRecovery)))
            .Where(static entry => entry.Damage > 0)];
    }

    private static ImmutableArray<ResourceAmount> Sum(Ruleset ruleset, IEnumerable<ResourceAmount> amounts) => YieldCalculator.Sum(ruleset, amounts);
}
