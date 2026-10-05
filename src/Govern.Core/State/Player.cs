using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.Science;

namespace Govern.Core.State;

/// <summary>
/// A participant and what they have discovered of the map. Resources belong to their cities (<see cref="Cities.City.Stock"/>).
/// </summary>
/// <param name="Id">Player identifier.</param>
/// <param name="Status">Whether the player still takes part.</param>
/// <param name="Explored">Every tile the player has ever seen, in discovery order; drives the fog of war.</param>
/// <param name="Nodes">
/// The science nodes the player has unlocked, in the order they were unlocked (G11a); <see langword="null"/> while there are none,
/// so games saved before the science tree read as having none.
/// </param>
/// <param name="KnownPosts">
/// Other players' cities whose trade post the player has seen together with the city (G17b), in the order they were seen: the
/// cities they can send deals to without seeing them now. <see langword="null"/> while there are none.
/// </param>
public sealed record Player(
    PlayerId Id,
    PlayerStatus Status,
    ImmutableArray<HexCoord> Explored,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<OwnedNode>? Nodes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<CityId>? KnownPosts = null)
{
    /// <summary>Other players' cities whose trade post the player knows of (empty when none).</summary>
    [JsonIgnore]
    public ImmutableArray<CityId> KnownPostList => KnownPosts ?? [];

    /// <summary>The science nodes the player has unlocked (empty when none).</summary>
    [JsonIgnore]
    public ImmutableArray<OwnedNode> NodeList => Nodes ?? [];
}
