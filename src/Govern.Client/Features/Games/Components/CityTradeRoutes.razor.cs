using System.Collections.Immutable;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Helpers;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The trade routes of the player's own city (G17), as their deals look from this city, with the answers and orders the player
/// may give: accept (cancelling older deals to make room) or decline a proposal, cancel a deal, close a route.
/// </summary>
public partial class CityTradeRoutes
{
    private readonly Dictionary<TradeRouteId, HashSet<TradeRouteId>> _cancel = [];

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The player's city.</summary>
    [Parameter, EditorRequired]
    public CityView City { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Whether the player may act now (their turn, game in progress).</summary>
    [Parameter]
    public bool CanAct { get; set; }

    /// <summary>Raised with the command a button sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    // The routes this city opened, then the ones it hosts.
    private List<TradeRouteView> Routes =>
        [.. View.TradeRouteList.Where(route => route.Home == City.Id), .. View.TradeRouteList.Where(route => route.Host == City.Id)];

    private string Title(TradeRouteView route) => route.Home == City.Id
        ? $"To {route.HostName}{Seat(route.HostOwner)}"
        : $"From {route.HomeName}{Seat(route.Owner)}";

    private string Seat(Core.State.PlayerId player) => player == View.Viewer ? string.Empty : $" (Seat {Seats.Number(View, player)})";

    // The other city of a route, for the cancel boxes.
    private string Partner(TradeRouteView route) => route.Home == City.Id ? route.HostName : route.HomeName;

    // How the route stands, with its deal from this city's side: what the city gives and what it gets each turn.
    private string Status(TradeRouteView route)
    {
        var terms = route.Deal is { } deal ? Terms(route, deal) : string.Empty;
        return route switch
        {
            { Opened: false } => "The trader is on its way to the trade post.",
            { Deal: null } => route.Owner == View.Viewer ? "The trader waits at the trade post for your proposal." : "No deal yet.",
            { Deal.Accepted: false } => $"Proposed: {terms}.",
            { Paused: true } => $"Paused, the trader's way is blocked: {terms}.",
            _ => $"Active: {terms}.",
        };
    }

    private string Terms(TradeRouteView route, TradeDeal deal)
    {
        var (gives, gets) = route.Home == City.Id ? (deal.Gives, deal.Receives) : (deal.Receives, deal.Gives);
        return $"gives {ResourceFormatter.Describe(Ruleset, gives)}, gets {ResourceFormatter.Describe(Ruleset, gets)} a turn";
    }

    // A proposal waiting at this city, for the player to answer.
    private bool Answerable(TradeRouteView route) =>
        route.Host == City.Id && route.HostOwner == View.Viewer && route.Deal is { Accepted: false };

    // The active deals the player may cancel to make room at this city: the ones it hosts and the ones it opened.
    private IEnumerable<TradeRouteView> Cancellable(TradeRouteView route) => View.TradeRouteList.Where(other =>
        other.IsActive && other.Id != route.Id && (other.Host == City.Id || (other.Home == City.Id && other.Owner == View.Viewer)));

    private bool Cancelling(TradeRouteView route, TradeRouteView other) => _cancel.TryGetValue(route.Id, out var set) && set.Contains(other.Id);

    private void Toggle(TradeRouteView route, TradeRouteView other, bool on)
    {
        var set = _cancel.TryGetValue(route.Id, out var existing) ? existing : _cancel[route.Id] = [];
        _ = on ? set.Add(other.Id) : set.Remove(other.Id);
    }

    private ImmutableArray<TradeRouteId> CancelList(TradeRouteView route) =>
        _cancel.TryGetValue(route.Id, out var set) ? [.. Cancellable(route).Select(static other => other.Id).Where(set.Contains)] : [];
}
