using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The answers and orders for one trade deal (G17b): accept, decline or counter an offer that waits for the player (an accept
/// may first cancel agreed deals of the answering city), withdraw an offer that waits for the other side, or cancel an agreed deal.
/// </summary>
public partial class DealAnswers
{
    private readonly HashSet<TradeDealId> _cancel = [];
    private bool _countering;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The deal, one the player is a side of.</summary>
    [Parameter, EditorRequired]
    public TradeDealView Deal { get; set; } = null!;

    /// <summary>The city whose Market tab shows the deal.</summary>
    [Parameter, EditorRequired]
    public CityId City { get; set; }

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Whether the player may act now (their turn, game in progress).</summary>
    [Parameter]
    public bool CanAct { get; set; }

    /// <summary>Raised with the command a button sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    // The agreed deals of the answering city the player may cancel to make room.
    private IEnumerable<TradeDealView> Cancellable => Deal.AnsweringCity is { } city
        ? View.TradeDealList.Where(other => other.IsAgreed && other.Id != Deal.Id && (other.Home == city || other.Host == city))
        : [];

    private ImmutableArray<TradeDealId> CancelList => [.. Cancellable.Select(static other => other.Id).Where(_cancel.Contains)];

    // The other city of a deal, seen from the answering city.
    private string Partner(TradeDealView other) => other.Home == Deal.AnsweringCity ? other.HostName : other.HomeName;

    private void Toggle(TradeDealView other, bool on) => _ = on ? _cancel.Add(other.Id) : _cancel.Remove(other.Id);

    private async Task CounterAsync(TradeTerms terms)
    {
        _countering = false;
        await Command.InvokeAsync(new AnswerDealCommand(Deal.Id, DealAnswer.Counter, terms.Gives, terms.Receives));
    }
}
