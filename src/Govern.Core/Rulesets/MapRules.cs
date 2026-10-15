using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Rulesets;

/// <summary>
/// The board (the ruleset's <c>map</c> section).
/// </summary>
/// <param name="Sizes">The size of a new generated map, one entry per player count the setup allows.</param>
/// <param name="MinSize">Smallest allowed width and height.</param>
/// <param name="MaxSize">Largest allowed width and height.</param>
/// <param name="DefaultTerrain">The terrain of every tile the generator leaves alone, and of every spawn tile.</param>
/// <param name="Spawn">Where players start.</param>
/// <param name="Generation">How a new map gets its other terrain.</param>
public sealed record MapRules(
    ImmutableArray<MapSizeRules> Sizes,
    int MinSize,
    int MaxSize,
    TerrainType DefaultTerrain,
    SpawnRules Spawn,
    MapGenerationRules Generation)
{
    /// <summary>The size of a new generated map for this many players.</summary>
    /// <param name="players">A player count the ruleset's setup allows.</param>
    /// <returns>The entry of <see cref="Sizes"/> for that count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No entry is for that many players.</exception>
    public MapSizeRules SizeFor(int players) =>
        Sizes.FirstOrDefault(size => size.Players == players)
        ?? throw new ArgumentOutOfRangeException(nameof(players), players, "The ruleset has no map size for this many players.");
}
