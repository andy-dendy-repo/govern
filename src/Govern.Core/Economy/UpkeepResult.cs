using System.Collections.Immutable;
using Govern.Core.Units;

namespace Govern.Core.Economy;

/// <summary>What a city's upkeep did to its stock.</summary>
/// <param name="Stock">The stock after paying, in the ruleset's order, without zeros.</param>
/// <param name="Paid">Everything taken from the stock, in the ruleset's order.</param>
/// <param name="UnpaidBuildings">Buildings the stock couldn't keep up, in the ruleset's order.</param>
/// <param name="UnpaidUnits">Units the stock couldn't keep up, in the order they were considered.</param>
public sealed record UpkeepResult(
    ImmutableArray<ResourceAmount> Stock,
    ImmutableArray<ResourceAmount> Paid,
    ImmutableArray<BuildingType> UnpaidBuildings,
    ImmutableArray<UnitId> UnpaidUnits);
