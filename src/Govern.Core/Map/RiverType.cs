using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// Kind of river on a tile edge: the id of a river in the game's ruleset (<see cref="Rulesets.Ruleset.Rivers"/>), such as
/// <c>stream</c>. Its crossing rules come from <see cref="Rulesets.Ruleset.River"/>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id.</param>
[JsonConverter(typeof(RiverTypeJsonConverter))]
public readonly record struct RiverType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
