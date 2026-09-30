using Govern.Core.Map;

namespace Govern.Core.Rulesets;

/// <summary>
/// The board (the ruleset's <c>map</c> section).
/// </summary>
/// <param name="DefaultWidth">Columns of a new map.</param>
/// <param name="DefaultHeight">Rows of a new map.</param>
/// <param name="MinSize">Smallest allowed width and height.</param>
/// <param name="MaxSize">Largest allowed width and height.</param>
/// <param name="DefaultTerrain">The terrain of every tile the generator leaves alone, and of every spawn tile.</param>
/// <param name="Spawn">Where players start.</param>
/// <param name="Generation">How a new map gets its other terrain.</param>
public sealed record MapRules(
    int DefaultWidth,
    int DefaultHeight,
    int MinSize,
    int MaxSize,
    TerrainType DefaultTerrain,
    SpawnRules Spawn,
    MapGenerationRules Generation);
