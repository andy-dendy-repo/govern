using Govern.Client.Features.Games.Models;
using Govern.Client.Helpers;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Writes the trade events of the game log (G17b) as sentences, for <see cref="GameLogFormatter"/>. Only the two sides of a
/// deal get these events. Cities are named from the cities the player sees, their trade partners or their deals.
/// </summary>
public static class TradeLogFormatter
{
    /// <summary>Describes one trade event.</summary>
    /// <param name="ruleset">The game's ruleset (resource names).</param>
    /// <param name="view">The player's current view (seats, deals and cities).</param>
    /// <param name="gameEvent">An event from the player's log.</param>
    /// <returns>The line; <see langword="null"/> for an event that isn't about trade.</returns>
    public static GameLogLine? Describe(Ruleset ruleset, PlayerView view, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);

        return gameEvent switch
        {
            TradeDealChangedEvent changed => Changed(view, changed),
            TradeExchangedEvent exchanged => Exchanged(ruleset, view, exchanged),
            _ => null,
        };
    }

    private static GameLogLine Changed(PlayerView view, TradeDealChangedEvent changed)
    {
        var (home, host) = (CityName(view, changed.DealId, changed.Home, home: true), CityName(view, changed.DealId, changed.Host, home: false));
        var owner = changed.Owner == view.Viewer ? "Your" : $"Seat {Seats.Number(view, changed.Owner)}'s";
        var text = changed.Change switch
        {
            TradeChange.Proposed => $"{owner} trader from {home} offered {host} a deal",
            TradeChange.Countered => $"New terms were offered for the deal between {home} and {host}",
            TradeChange.Accepted => $"The deal between {home} and {host} was accepted: the trader sets off for {host}",
            TradeChange.Declined => $"The deal between {home} and {host} was declined",
            TradeChange.Started => $"{owner} trader reached {host}: the deal with {home} is active",
            TradeChange.Cancelled => $"The deal between {home} and {host} was cancelled",
            TradeChange.Broken => $"The deal between {home} and {host} broke: its trader was lost",
            _ => $"The deal between {home} and {host} ended: a city changed hands",
        };
        return new GameLogLine(text, CityTile(view, changed.Host), changed.Owner == view.Viewer);
    }

    private static GameLogLine Exchanged(Ruleset ruleset, PlayerView view, TradeExchangedEvent exchanged)
    {
        var (home, host) = (CityName(view, exchanged.DealId, exchanged.Home, home: true), CityName(view, exchanged.DealId, exchanged.Host, home: false));
        var text = (exchanged.HomeShort, exchanged.HostShort) switch
        {
            (false, false) => $"{home} gave {ResourceFormatter.Describe(ruleset, exchanged.Gives)} to {host} for {ResourceFormatter.Describe(ruleset, exchanged.Receives)}",
            (true, true) => $"No trade between {home} and {host} this turn: neither city had its part",
            (true, false) => $"No trade between {home} and {host} this turn: {home} lacked {ResourceFormatter.Describe(ruleset, exchanged.Gives)}",
            _ => $"No trade between {home} and {host} this turn: {host} lacked {ResourceFormatter.Describe(ruleset, exchanged.Receives)}",
        };
        return new GameLogLine(text, CityTile(view, exchanged.Host), exchanged.Owner == view.Viewer);
    }

    private static string CityName(PlayerView view, TradeDealId deal, CityId city, bool home) =>
        view.Cities.FirstOrDefault(seen => seen.Id == city)?.Name
        ?? view.TradePartnerList.FirstOrDefault(partner => partner.Id == city)?.Name
        ?? view.TradeDealList.FirstOrDefault(known => known.Id == deal) switch
        {
            { } known => home ? known.HomeName : known.HostName,
            null => "a city",
        };

    private static Core.Map.HexCoord? CityTile(PlayerView view, CityId city) => view.Cities.FirstOrDefault(seen => seen.Id == city)?.Position;
}
