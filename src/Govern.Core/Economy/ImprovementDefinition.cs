using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Economy;

/// <summary>
/// Rules data for one <see cref="ImprovementType"/>, from the game's ruleset. A builder builds it on a tile of its city's work
/// area whose terrain is listed, at most one per tile; each turn it adds its yields to the city owner's stockpile.
/// </summary>
/// <param name="Id">The improvement this definition describes.</param>
/// <param name="Terrains">The terrains it can be built on.</param>
/// <param name="Cost">Production points needed to build it.</param>
/// <param name="Yields">What it adds to its owner's stockpile at the end of each of their turns.</param>
public sealed record ImprovementDefinition(ImprovementType Id, ImmutableArray<TerrainType> Terrains, int Cost, ImmutableArray<ResourceAmount> Yields);
