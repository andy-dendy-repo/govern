using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A trade route as one of its two sides sees it (G17). Its owner sees all of its routes; a host city's owner sees the routes
/// at their cities once opened. Neither side sees the other's other routes. What the trader learned at the host's trade post
/// (its market and demand levels) is shown to the route's owner only, while the trader stands there.
/// </summary>
/// <param name="Id">Route identifier.</param>
/// <param name="Owner">The route's owner (the home city's owner).</param>
/// <param name="Home">The home city.</param>
/// <param name="HomeName">The home city's name.</param>
/// <param name="Host">The host city.</param>
/// <param name="HostName">The host city's name.</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Opened">Whether the trader has reached the host's trade post.</param>
/// <param name="Paused">Whether the trader found no way on its last walk (an accepted deal swaps nothing meanwhile); the owner only.</param>
/// <param name="Deal">The proposed or accepted deal, if any.</param>
/// <param name="Trader">The trader serving the route; the owner only.</param>
/// <param name="HostPost">The host's trade post tile, where proposals are made; the owner only.</param>
/// <param name="HostMarket">
/// The host's market with what its other deals use taken off each cap; the owner only, while the route is open and its trader
/// stands at the host's trade post.
/// </param>
/// <param name="HostDemand">The host's demand levels that differ from the base level; the owner only, when <c>HostMarket</c> is shown.</param>
public sealed record TradeRouteView(
    TradeRouteId Id,
    PlayerId Owner,
    CityId Home,
    string HomeName,
    CityId Host,
    string HostName,
    PlayerId HostOwner,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Opened = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Paused = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TradeDeal? Deal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitId? Trader = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HexCoord? HostPost = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<MarketOrder>? HostMarket = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceDemand>? HostDemand = null)
{
    /// <summary>Whether the route carries an accepted deal.</summary>
    [JsonIgnore]
    public bool IsActive => Deal is { Accepted: true };

    // The route as a viewer sees it; null when the viewer is neither side, or hosts a route whose trader hasn't arrived yet.
    internal static TradeRouteView? From(Ruleset ruleset, GameState state, TradeRoute route, PlayerId viewer)
    {
        var owner = route.Owner == viewer;
        if (!owner && (route.HostOwner != viewer || !route.Opened))
        {
            return null;
        }

        var home = state.FindCity(route.Home)!;
        var host = state.FindCity(route.Host)!;
        var view = new TradeRouteView(route.Id, route.Owner, home.Id, home.Name, host.Id, host.Name, route.HostOwner, route.Opened, Deal: route.Deal);
        if (!owner)
        {
            return view;
        }

        var post = TradeRule.PostTile(ruleset, state.Map, host.Id);
        var learned = route.Opened && post is { } postTile && route.Trader is { } traderId
            && state.FindUnit(traderId) is { } trader && TradeRule.IsAt(trader.Position, postTile);
        return view with
        {
            Paused = route.Paused,
            Trader = route.Trader,
            HostPost = post,
            HostMarket = learned ? TradeRule.FreeMarket(state, host, route.Id) : null,
            HostDemand = learned && !host.DemandList.IsEmpty ? host.DemandList : null,
        };
    }
}
