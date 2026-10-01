using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

/// <summary>
/// Kind of tile improvement a builder builds in a city's territory: the id of an improvement in the game's ruleset (<see cref="Rulesets.Ruleset.Improvements"/>), such as <c>farm</c>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id. Values from commands are untrusted: look them up with <see cref="Rulesets.Ruleset.FindImprovement"/>.</param>
[JsonConverter(typeof(ImprovementTypeJsonConverter))]
public readonly record struct ImprovementType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
