using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.State;

/// <summary>
/// A participant, what they have discovered of the map, and their resources.
/// </summary>
/// <param name="Id">Player identifier.</param>
/// <param name="Status">Whether the player still takes part.</param>
/// <param name="Explored">Every tile the player has ever seen, in discovery order; drives the fog of war.</param>
/// <param name="Resources">
/// The player's stockpile, one entry per resource they have collected, in the ruleset's order; <see langword="null"/> while
/// empty, so the JSON leaves it out.
/// </param>
public sealed record Player(
    PlayerId Id,
    PlayerStatus Status,
    ImmutableArray<HexCoord> Explored,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceAmount>? Resources = null)
{
    /// <summary>The stockpile (empty when nothing has been collected).</summary>
    [JsonIgnore]
    public ImmutableArray<ResourceAmount> Stockpile => Resources ?? [];
}
