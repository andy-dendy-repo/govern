using System.Collections.Immutable;

namespace Govern.Core.Rulesets;

/// <summary>
/// How a new map gets its terrain (the ruleset's <c>map.generation</c> section). The generator starts from
/// <see cref="MapRules.DefaultTerrain"/> and lays down patches of each listed terrain, in order.
/// </summary>
/// <param name="Patches">The terrains to lay down, each with its share of the map and its patch size.</param>
public sealed record MapGenerationRules(ImmutableArray<TerrainPatchRules> Patches);
