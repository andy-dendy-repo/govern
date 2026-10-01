using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

/// <summary>
/// Kind of resource: the id of a resource in the game's ruleset (<see cref="Rulesets.Ruleset.Resources"/>), such as <c>food</c>. JSON holds the plain id.
/// </summary>
/// <param name="Id">The ruleset id. Values from commands are untrusted: look them up with <see cref="Rulesets.Ruleset.FindResource"/>.</param>
[JsonConverter(typeof(ResourceTypeJsonConverter))]
public readonly record struct ResourceType(string Id)
{
    /// <summary>The id.</summary>
    /// <returns><see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
