using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Economy;

/// <summary>
/// Rules data for one <see cref="BuildingType"/>, from the game's ruleset. A city produces it with its production, at most one
/// of each per city; it stays when the city changes hands.
/// </summary>
/// <param name="Id">The building this definition describes.</param>
/// <param name="Cost">Production points needed to produce it.</param>
/// <param name="ResourceCost">Resources the city pays in full when it begins producing it (may be empty).</param>
/// <param name="Upkeep">Resources its city pays each turn to keep it working (may be empty).</param>
/// <param name="Yields">What it adds to its city's stock at the end of each of their turns.</param>
/// <param name="Storage">
/// The room it adds to its city's storage (<see cref="Rulesets.CityRules.Storage"/>) while it works (may be empty); an upgrade
/// replaces the building it upgrades, so it lists its own full amounts.
/// </param>
/// <param name="Production">Extra production points per turn for its city (0 for none).</param>
/// <param name="RequiresAdjacentTerrain">
/// Terrains of which at least one must be next to the building's tile for it to be produced there; empty when it can stand anywhere.
/// </param>
/// <param name="Upgrades">
/// The building this one upgrades (it replaces it on its tile); a builder upgrades it, a city never produces it from scratch.
/// <see langword="null"/> for a building a city produces.
/// </param>
public sealed record BuildingDefinition(
    BuildingType Id,
    int Cost,
    ImmutableArray<ResourceAmount> ResourceCost,
    ImmutableArray<ResourceAmount> Upkeep,
    ImmutableArray<ResourceAmount> Yields,
    ImmutableArray<ResourceAmount> Storage,
    int Production,
    ImmutableArray<TerrainType> RequiresAdjacentTerrain,
    BuildingType? Upgrades = null);
