using Govern.Core.Map;

namespace Govern.Core.Rulesets;

/// <summary>
/// One terrain the map generator lays down (an entry of <c>map.generation.patches</c>).
/// </summary>
/// <param name="Terrain">The terrain to lay down.</param>
/// <param name="SharePerMille">The share of the map's tiles it aims to cover, in thousandths (150 = 15 %).</param>
/// <param name="PatchSize">The number of tiles each patch grows to, when there is room.</param>
public sealed record TerrainPatchRules(TerrainType Terrain, int SharePerMille, int PatchSize);
