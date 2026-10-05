using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;

namespace Govern.Core.Visibility;

/// <summary>
/// Another player's city the viewer can send trade deals to (G17b): one whose trade post they have seen
/// (<see cref="Player.KnownPosts"/>), seen or not now. It shows what a fair offer needs: the city's market and demand levels.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">The city's owner.</param>
/// <param name="Name">The city's name.</param>
/// <param name="TradePost">Its trade post tile, where traders go.</param>
/// <param name="Market">
/// What the city sells and buys, with what its agreed deals the viewer is not a side of use taken off each cap; the viewer's own
/// deals with it are in <see cref="PlayerView.TradeDeals"/>. <see langword="null"/> when it trades nothing.
/// </param>
/// <param name="Demand">Its demand levels that differ from the base level; <see langword="null"/> when none does.</param>
public sealed record TradePartnerView(
    CityId Id,
    PlayerId Owner,
    string Name,
    HexCoord TradePost,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<MarketOrder>? Market = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceDemand>? Demand = null)
{
    /// <summary>The city's market (empty when it trades nothing).</summary>
    [JsonIgnore]
    public ImmutableArray<MarketOrder> MarketList => Market ?? [];

    /// <summary>The city's demand levels that differ from the base level (empty when none does).</summary>
    [JsonIgnore]
    public ImmutableArray<ResourceDemand> DemandList => Demand ?? [];
}
