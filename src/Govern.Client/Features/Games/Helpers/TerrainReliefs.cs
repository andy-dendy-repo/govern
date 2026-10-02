using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The 3D board's look per ruleset terrain. Presentation only, so it lives in the client, not in the ruleset; like the terrain
/// tokens in <c>tokens.css</c>, every ruleset terrain needs an entry (<c>TerrainReliefsTests</c>). An unknown terrain is drawn flat.
/// </summary>
public static class TerrainReliefs
{
    /// <summary>The relief drawn for a terrain that has no entry.</summary>
    public static readonly TileRelief Default = new(0.2, TileShape.Flat);

    /// <summary>The relief of an unexplored tile: low, under the clouds.</summary>
    public static readonly TileRelief Unexplored = new(0.05, TileShape.Flat);

    private static readonly Dictionary<string, TileRelief> ById = new(StringComparer.Ordinal)
    {
        ["plains"] = Default,
        ["desert"] = new(0.2, TileShape.Dunes),
        ["forest"] = new(0.25, TileShape.Trees),
        ["hills"] = new(0.45, TileShape.Hills),
        ["mountains"] = new(0.6, TileShape.Peaks),
        ["lake"] = new(0.05, TileShape.Water),
    };

    /// <summary>The terrain ids that have an entry.</summary>
    public static IEnumerable<string> Known => ById.Keys;

    /// <summary>The relief of a terrain.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>Its relief, or <see cref="Default"/> when it has no entry.</returns>
    public static TileRelief For(TerrainType terrain) => ById.GetValueOrDefault(terrain.Id, Default);
}
