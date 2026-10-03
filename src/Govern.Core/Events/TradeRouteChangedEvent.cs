using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// Something happened to a trade route (G17). Only the route's owner and the host city's owner receive it, and the host's owner
/// only once the route was opened (the host never learns of a route whose trader hasn't arrived).
/// </summary>
/// <param name="RouteId">The route.</param>
/// <param name="Owner">The route's owner (the home city's owner).</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Home">The home city.</param>
/// <param name="Host">The host city.</param>
/// <param name="Change">What happened.</param>
/// <param name="Unopened">Whether the route had not been opened yet (closed before its trader arrived); hidden from the host.</param>
public sealed record TradeRouteChangedEvent(
    TradeRouteId RouteId,
    PlayerId Owner,
    PlayerId HostOwner,
    CityId Home,
    CityId Host,
    TradeChange Change,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Unopened = false) : GameEvent;
