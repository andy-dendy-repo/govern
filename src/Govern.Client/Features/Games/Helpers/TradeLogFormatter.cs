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
/// Writes the trade events of the game log (G17) as sentences, for <see cref="GameLogFormatter"/>. Only the two sides of a
/// route get these events. Cities are named from the player's routes or the cities they see.
/// </summary>
public static class TradeLogFormatter
{
    /// <summary>Describes one trade event.</summary>
    /// <param name="ruleset">The game's ruleset (resource names).</param>
    /// <param name="view">The player's current view (seats, routes and cities).</param>
    /// <param name="gameEvent">An event from the player's log.</param>
    /// <returns>The line; <see langword="null"/> for an event that isn't about trade.</returns>
    public static GameLogLine? Describe(Ruleset ruleset, PlayerView view, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);

        return gameEvent switch
        {
            TradeRouteChangedEvent changed => Changed(view, changed),
            TradeExchangedEvent exchanged => Exchanged(ruleset, view, exchanged),
            _ => null,
        };
    }

    private static GameLogLine Changed(PlayerView view, TradeRouteChangedEvent changed)
    {
        var (home, host) = (CityName(view, changed.RouteId, changed.Home, home: true), CityName(view, changed.RouteId, changed.Host, home: false));
        var owner = changed.Owner == view.Viewer ? "Your" : $"Seat {Seats.Number(view, changed.Owner)}'s";
        var text = changed.Change switch
        {
            TradeChange.Opened => $"{owner} trader reached {host}: the trade route from {home} is open",
            TradeChange.Proposed => $"{owner} trader proposed a deal from {home} at {host}",
            TradeChange.Accepted => $"The deal between {home} and {host} was accepted",
            TradeChange.Declined => $"The deal proposed from {home} at {host} was declined",
            TradeChange.Cancelled => $"The deal between {home} and {host} was cancelled",
            TradeChange.Broken => $"The deal between {home} and {host} broke: its trader was lost",
            _ => $"The trade route between {home} and {host} closed",
        };
        return new GameLogLine(text, CityTile(view, changed.Host), changed.Owner == view.Viewer);
    }

    private static GameLogLine Exchanged(Ruleset ruleset, PlayerView view, TradeExchangedEvent exchanged)
    {
        var (home, host) = (CityName(view, exchanged.RouteId, exchanged.Home, home: true), CityName(view, exchanged.RouteId, exchanged.Host, home: false));
        var text = (exchanged.HomeShort, exchanged.HostShort) switch
        {
            (false, false) => $"{home} gave {ResourceFormatter.Describe(ruleset, exchanged.Gives)} to {host} for {ResourceFormatter.Describe(ruleset, exchanged.Receives)}",
            (true, true) => $"No trade between {home} and {host} this turn: neither city had its part",
            (true, false) => $"No trade between {home} and {host} this turn: {home} lacked {ResourceFormatter.Describe(ruleset, exchanged.Gives)}",
            _ => $"No trade between {home} and {host} this turn: {host} lacked {ResourceFormatter.Describe(ruleset, exchanged.Receives)}",
        };
        return new GameLogLine(text, CityTile(view, exchanged.Host), exchanged.Owner == view.Viewer);
    }

    private static string CityName(PlayerView view, TradeRouteId route, CityId city, bool home) =>
        view.Cities.FirstOrDefault(seen => seen.Id == city)?.Name
        ?? view.TradeRouteList.FirstOrDefault(known => known.Id == route) switch
        {
            { } known => home ? known.HomeName : known.HostName,
            null => "a city",
        };

    private static Core.Map.HexCoord? CityTile(PlayerView view, CityId city) => view.Cities.FirstOrDefault(seen => seen.Id == city)?.Position;
}
