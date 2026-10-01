using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

/// <summary>
/// Kind of city building, produced by a city and at most one of each per city: the id of a building in the game's ruleset (<see cref="Rulesets.Ruleset.Buildings"/>), such as <c>campus</c>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id. Values from commands are untrusted: look them up with <see cref="Rulesets.Ruleset.FindBuilding"/>.</param>
[JsonConverter(typeof(BuildingTypeJsonConverter))]
public readonly record struct BuildingType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
