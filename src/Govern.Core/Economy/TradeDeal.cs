using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Economy;

/// <summary>
/// A trade deal (G17b): one trader's offer from its home city to a host city, and once agreed its swap. The trader's owner
/// proposes it from home; the side that must answer accepts, declines or counters with other terms. Once agreed the trader
/// walks to the host's trade post; the deal turns active when it gets there, and from then on the trader shuttles between the
/// two posts while the goods swap every turn. Either side can cancel it; it ends when the trader dies or a city changes hands.
/// </summary>
/// <param name="Id">Deal identifier.</param>
/// <param name="Owner">The trader's owner (the home city's owner).</param>
/// <param name="Home">The trader's home city.</param>
/// <param name="Host">The city at the other end.</param>
/// <param name="HostOwner">The host city's owner when the deal was proposed; the deal ends when the host changes hands.</param>
/// <param name="Trader">The trader the deal belongs to.</param>
/// <param name="Terms">The terms on the table, or agreed.</param>
/// <param name="Status">Where the deal stands.</param>
/// <param name="ActiveTurn">The round it turned active in; older deals trade first.</param>
/// <param name="Paused">Whether the trader found no way on its last walk; an active deal swaps nothing while paused.</param>
public sealed record TradeDeal(
    TradeDealId Id,
    PlayerId Owner,
    CityId Home,
    CityId Host,
    PlayerId HostOwner,
    UnitId Trader,
    TradeTerms Terms,
    TradeDealStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ActiveTurn = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Paused = false)
{
    /// <summary>Whether both sides agreed (the trader is on its way or the deal is active), so the terms use both markets.</summary>
    [JsonIgnore]
    public bool IsAgreed => Status is TradeDealStatus.Travelling or TradeDealStatus.Active;

    /// <summary>Whether the deal swaps goods: active and not paused.</summary>
    [JsonIgnore]
    public bool IsSwapping => Status == TradeDealStatus.Active && !Paused;

    /// <summary>The player who must answer the terms on the table; <see langword="null"/> once agreed.</summary>
    [JsonIgnore]
    public PlayerId? Answerer => Status switch
    {
        TradeDealStatus.Proposed => HostOwner,
        TradeDealStatus.Countered => Owner,
        _ => null,
    };

    /// <summary>The city of the side that must answer; <see langword="null"/> once agreed.</summary>
    [JsonIgnore]
    public CityId? AnsweringCity => Status switch
    {
        TradeDealStatus.Proposed => Host,
        TradeDealStatus.Countered => Home,
        _ => null,
    };
}
