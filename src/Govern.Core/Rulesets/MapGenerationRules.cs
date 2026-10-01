using System.Collections.Immutable;

namespace Govern.Core.Rulesets;

/// <summary>
/// How a new map gets its terrain and rivers (the ruleset's <c>map.generation</c> section). The generator starts from
/// <see cref="MapRules.DefaultTerrain"/>, lays down patches of each listed terrain, in order, and then the rivers.
/// </summary>
/// <param name="Patches">The terrains to lay down, each with its share of the map and its patch size.</param>
/// <param name="Rivers">The rivers to lay down.</param>
public sealed record MapGenerationRules(ImmutableArray<TerrainPatchRules> Patches, RiverGenerationRules Rivers);
