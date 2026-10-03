using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Economy;

/// <summary>
/// A trade route between two cities' trade posts (G17), opened by a trader of the <see cref="Owner"/> sent from
/// <see cref="Home"/>. It exists once the trader reaches the host's trade post (<see cref="Opened"/>); the trader then proposes a
/// <see cref="Deal"/>, and once the host accepts it walks between the two posts and the deal swaps goods each turn. A killed
/// trader breaks the deal but leaves the route; a captured city ends the route.
/// </summary>
/// <param name="Id">Route identifier.</param>
/// <param name="Owner">The player whose trader opened it (the home city's owner).</param>
/// <param name="Home">The city the route was opened from.</param>
/// <param name="Host">The city at the other end.</param>
/// <param name="HostOwner">The host city's owner when the route was opened; the route ends when the host changes hands.</param>
/// <param name="Opened">Whether the trader has reached the host's trade post.</param>
/// <param name="Trader">The trader serving the route; <see langword="null"/> when it was killed or the route waits for a new one.</param>
/// <param name="Deal">The proposed or accepted deal; <see langword="null"/> when there is none.</param>
/// <param name="Paused">Whether the trader found no way on its last walk; an accepted deal swaps nothing while paused.</param>
public sealed record TradeRoute(
    TradeRouteId Id,
    PlayerId Owner,
    CityId Home,
    CityId Host,
    PlayerId HostOwner,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Opened = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitId? Trader = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TradeDeal? Deal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Paused = false)
{
    /// <summary>Whether the route carries an accepted deal.</summary>
    [JsonIgnore]
    public bool IsActive => Deal is { Accepted: true };
}
