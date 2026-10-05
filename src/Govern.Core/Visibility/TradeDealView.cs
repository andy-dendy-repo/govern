using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A trade deal as one of its two sides sees it (G17b). Both sides see the deal, its terms and where it stands; neither sees
/// the other's other deals. The trader and whether its walk is paused are shown to the trader's owner only.
/// </summary>
/// <param name="Id">Deal identifier.</param>
/// <param name="Owner">The trader's owner (the home city's owner).</param>
/// <param name="Home">The home city.</param>
/// <param name="HomeName">The home city's name.</param>
/// <param name="Host">The host city.</param>
/// <param name="HostName">The host city's name.</param>
/// <param name="HostOwner">The host city's owner.</param>
/// <param name="Terms">The terms on the table, or agreed.</param>
/// <param name="Status">Where the deal stands.</param>
/// <param name="Trader">The trader carrying the deal; the owner only.</param>
/// <param name="Paused">Whether the trader found no way on its last walk (an active deal swaps nothing meanwhile); the owner only.</param>
public sealed record TradeDealView(
    TradeDealId Id,
    PlayerId Owner,
    CityId Home,
    string HomeName,
    CityId Host,
    string HostName,
    PlayerId HostOwner,
    TradeTerms Terms,
    TradeDealStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnitId? Trader = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Paused = false)
{
    /// <summary>Whether both sides agreed (the trader is on its way or the deal is active).</summary>
    [JsonIgnore]
    public bool IsAgreed => Status is TradeDealStatus.Travelling or TradeDealStatus.Active;

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

    // The deal as a viewer sees it; null when the viewer is neither side.
    internal static TradeDealView? From(GameState state, TradeDeal deal, PlayerId viewer)
    {
        var owner = deal.Owner == viewer;
        if (!owner && deal.HostOwner != viewer)
        {
            return null;
        }

        var home = state.FindCity(deal.Home)!;
        var host = state.FindCity(deal.Host)!;
        var view = new TradeDealView(deal.Id, deal.Owner, home.Id, home.Name, host.Id, host.Name, deal.HostOwner, deal.Terms, deal.Status);
        return owner ? view with { Trader = deal.Trader, Paused = deal.Paused } : view;
    }
}
