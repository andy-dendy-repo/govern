using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The 3D board's look per ruleset terrain. Presentation only, so it lives in the client, not in the ruleset; like the terrain
/// tokens in <c>tokens.css</c>, every ruleset terrain needs an entry (<c>TerrainReliefsTests</c>). An unknown terrain is drawn flat.
/// </summary>
public static class TerrainReliefs
{
    /// <summary>The relief drawn for a terrain that has no entry.</summary>
    public static readonly TileRelief Default = new(0.08, TileShape.Flat);

    /// <summary>The relief of an unexplored tile: flat, under the mist.</summary>
    public static readonly TileRelief Unexplored = new(0.02, TileShape.Flat);

    /// <summary>The height of the water over a <see cref="TileShape.Water"/> tile (<c>WATER_LEVEL</c> in <c>ground.js</c>), just under the lowest land, so shores run down to it.</summary>
    public const double WaterLevel = 0.07;

    private static readonly Dictionary<string, TileRelief> ById = new(StringComparer.Ordinal)
    {
        ["plains"] = Default,
        ["desert"] = new(0.08, TileShape.Dunes),
        ["forest"] = new(0.1, TileShape.Trees),
        ["hills"] = new(0.2, TileShape.Hills),
        ["mountains"] = new(0.28, TileShape.Peaks),
        ["lake"] = new(0.03, TileShape.Water),
    };

    /// <summary>The terrain ids that have an entry.</summary>
    public static IEnumerable<string> Known => ById.Keys;

    /// <summary>The relief of a terrain.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>Its relief, or <see cref="Default"/> when it has no entry.</returns>
    public static TileRelief For(TerrainType terrain) => ById.GetValueOrDefault(terrain.Id, Default);

    /// <summary>The relief of a tile as the player knows it.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>Its terrain's relief, or <see cref="Unexplored"/> when the player hasn't seen it.</returns>
    public static TileRelief For(TileView tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Terrain is { } terrain ? For(terrain) : Unexplored;
    }

    /// <summary>The height of a tile's surface: its top, or the water over it.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>The height in board units.</returns>
    public static double Surface(TileView tile)
    {
        var relief = For(tile);
        return relief.Shape == TileShape.Water ? Math.Max(relief.Height, WaterLevel) : relief.Height;
    }
}
