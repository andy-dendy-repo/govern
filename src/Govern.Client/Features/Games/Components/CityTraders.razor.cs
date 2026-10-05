using Govern.Client.Features.Games.Helpers;
using Govern.Client.Helpers;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The traders of the player's own city and the deals other cities' traders bring to it (G17b), with what the player can do
/// about each: suggest a deal, withdraw or cancel one, or answer an offer (<see cref="DealAnswers"/>).
/// </summary>
public partial class CityTraders
{
    private UnitId? _suggesting;

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

    // The first unit type that trades, for the "train one" hint.
    private UnitType TraderType => Ruleset.Units.First(static unit => unit.CanTrade).Id;

    // The player's traders whose home is this city, by id.
    private List<UnitView> Traders => [.. View.Units.Where(unit => unit.Owner == View.Viewer && unit.HomeCity == City.Id && Ruleset.Unit(unit.Type).CanTrade)];

    // Deals this city hosts for another city's trader.
    private List<TradeDealView> Hosted => [.. View.TradeDealList.Where(deal => deal.Host == City.Id && deal.HostOwner == View.Viewer)];

    private TradeDealView? DealOf(UnitView trader) => View.TradeDealList.FirstOrDefault(deal => deal.Trader == trader.Id);

    // How a trader and its deal stand, from the home city's side.
    private string TraderStatus(TradeDealView? deal) => deal switch
    {
        null => "Free: no deal.",
        { Status: TradeDealStatus.Proposed } => $"Offered to {Host(deal)}: {Terms(deal, home: true)}. Waiting for their answer.",
        { Status: TradeDealStatus.Countered } => $"{Host(deal)} offers instead: {Terms(deal, home: true)}.",
        { Paused: true } => $"Paused, no known way to {Host(deal)}: {Terms(deal, home: true)}.",
        { Status: TradeDealStatus.Travelling } => $"On its way to {Host(deal)}; the deal starts when it arrives: {Terms(deal, home: true)}.",
        _ => $"Active with {Host(deal)}: {Terms(deal, home: true)}.",
    };

    // How a deal another city's trader brings stands, from this (host) city's side.
    private string HostedStatus(TradeDealView deal) => deal.Status switch
    {
        TradeDealStatus.Proposed => $"{Home(deal)} offers: {Terms(deal, home: false)}.",
        TradeDealStatus.Countered => $"You offered {Home(deal)}: {Terms(deal, home: false)}. Waiting for their answer.",
        TradeDealStatus.Travelling => $"{Home(deal)}'s trader is on its way here; the deal starts when it arrives: {Terms(deal, home: false)}.",
        _ => $"Active with {Home(deal)}: {Terms(deal, home: false)}.",
    };

    private string Host(TradeDealView deal) => $"{deal.HostName}{Seat(deal.HostOwner)}";

    private string Home(TradeDealView deal) => $"{deal.HomeName}{Seat(deal.Owner)}";

    private string Seat(PlayerId player) => player == View.Viewer ? string.Empty : $" (Seat {Seats.Number(View, player)})";

    // What this city gives and gets each turn.
    private string Terms(TradeDealView deal, bool home)
    {
        var (gives, gets) = home ? (deal.Terms.Gives, deal.Terms.Receives) : (deal.Terms.Receives, deal.Terms.Gives);
        return $"you give {ResourceFormatter.Describe(Ruleset, gives)}, get {ResourceFormatter.Describe(Ruleset, gets)} a turn";
    }

    private async Task ProposeAsync(UnitId trader, CityId host, TradeTerms terms)
    {
        _suggesting = null;
        await Command.InvokeAsync(new ProposeDealCommand(trader, host, terms.Gives, terms.Receives));
    }
}
