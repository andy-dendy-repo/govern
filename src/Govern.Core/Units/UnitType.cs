using System.Text.Json.Serialization;

namespace Govern.Core.Units;

/// <summary>
/// Kind of unit: the id of a unit in the game's ruleset (<see cref="Rulesets.Ruleset.Units"/>), such as <c>settler</c>. Its stats
/// and abilities come from <see cref="Rulesets.Ruleset.Unit"/>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id. Values from commands are untrusted: look them up with <see cref="Rulesets.Ruleset.FindUnit"/>.</param>
[JsonConverter(typeof(UnitTypeJsonConverter))]
public readonly record struct UnitType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
