using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A trade route's deal swapped goods at the end of the host owner's turn (G17), or swapped nothing because a side didn't have
/// its part. Only the route's owner and the host city's owner receive it.
/// </summary>
/// <param name="RouteId">The route.</param>
/// <param name="Owner">The route's owner (the home city's owner).</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Home">The home city.</param>
/// <param name="Host">The host city.</param>
/// <param name="Gives">What the home city gave (or was to give).</param>
/// <param name="Receives">What the host city gave back (or was to give).</param>
/// <param name="HomeShort">Whether the home city lacked its part, so nothing was swapped.</param>
/// <param name="HostShort">Whether the host city lacked its part, so nothing was swapped.</param>
public sealed record TradeExchangedEvent(
    TradeRouteId RouteId,
    PlayerId Owner,
    PlayerId HostOwner,
    CityId Home,
    CityId Host,
    ImmutableArray<ResourceAmount> Gives,
    ImmutableArray<ResourceAmount> Receives,
    bool HomeShort,
    bool HostShort) : GameEvent;
