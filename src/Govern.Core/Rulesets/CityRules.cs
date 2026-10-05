using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

/// <summary>
/// Cities (the ruleset's <c>cities</c> section).
/// </summary>
/// <param name="Sight">Radius, in tiles, a city reveals around itself.</param>
/// <param name="MinimumSpacing">The smallest hex distance between two cities.</param>
/// <param name="ProductionPerTurn">Production points a city adds at the end of its owner's turn.</param>
/// <param name="InitialProduction">What a new city builds first.</param>
/// <param name="SuggestedNames">
/// Text keys of the names the client suggests for a new city (at least one). The player picks the name; these are only offered.
/// </param>
/// <param name="CaptureResetsProduction">Whether a captured city's production progress goes back to 0.</param>
/// <param name="Yields">What a city gives every turn on top of its tiles and buildings (may be empty).</param>
/// <param name="StartingStock">The resources a new city starts with (may be empty).</param>
/// <param name="Storage">
/// How much of each stocked resource a city can hold before its buildings add room (<see cref="BuildingDefinition.Storage"/>):
/// one entry for every resource but the science one. What doesn't fit is lost at the end of every turn.
/// </param>
public sealed record CityRules(
    int Sight,
    int MinimumSpacing,
    int ProductionPerTurn,
    UnitType InitialProduction,
    ImmutableArray<string> SuggestedNames,
    bool CaptureResetsProduction,
    ImmutableArray<ResourceAmount> Yields,
    ImmutableArray<ResourceAmount> StartingStock,
    ImmutableArray<ResourceAmount> Storage);
