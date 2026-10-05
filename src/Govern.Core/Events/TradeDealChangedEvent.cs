using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// Something happened to a trade deal (G17b). Only the deal's two sides receive it: the trader's owner and the host city's owner.
/// </summary>
/// <param name="DealId">The deal.</param>
/// <param name="Owner">The trader's owner (the home city's owner).</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Home">The home city.</param>
/// <param name="Host">The host city.</param>
/// <param name="Change">What happened.</param>
public sealed record TradeDealChangedEvent(
    TradeDealId DealId,
    PlayerId Owner,
    PlayerId HostOwner,
    CityId Home,
    CityId Host,
    TradeChange Change) : GameEvent;
