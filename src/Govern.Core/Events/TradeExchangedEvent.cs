using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A trade deal swapped goods at the end of the host owner's turn (G17b), or swapped nothing because a side didn't have
/// its part. Only the deal's two sides receive it.
/// </summary>
/// <param name="DealId">The deal.</param>
/// <param name="Owner">The trader's owner (the home city's owner).</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Home">The home city.</param>
/// <param name="Host">The host city.</param>
/// <param name="Gives">What the home city gave (or was to give).</param>
/// <param name="Receives">What the host city gave back (or was to give).</param>
/// <param name="HomeShort">Whether the home city lacked its part, so nothing was swapped.</param>
/// <param name="HostShort">Whether the host city lacked its part, so nothing was swapped.</param>
public sealed record TradeExchangedEvent(
    TradeDealId DealId,
    PlayerId Owner,
    PlayerId HostOwner,
    CityId Home,
    CityId Host,
    ImmutableArray<ResourceAmount> Gives,
    ImmutableArray<ResourceAmount> Receives,
    bool HomeShort,
    bool HostShort) : GameEvent;
