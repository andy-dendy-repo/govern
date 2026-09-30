using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// Kind of terrain on a tile: the id of a terrain in the game's ruleset (<see cref="Rulesets.Ruleset.Terrains"/>), such as
/// <c>plains</c>. Its movement rules come from <see cref="Rulesets.Ruleset.Terrain"/>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id.</param>
[JsonConverter(typeof(TerrainTypeJsonConverter))]
public readonly record struct TerrainType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
