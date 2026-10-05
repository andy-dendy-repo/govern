using System.Collections.Immutable;
using System.Globalization;
using Govern.Client.Helpers;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The terms form of a trade deal (G17b): the city the deal goes to (unless <see cref="FixedHost"/> is set), one resource the
/// home city gives and one it asks for each turn. <see cref="Send"/> gets the host and the terms; <see cref="Check"/> says why
/// they would be rejected.
/// </summary>
public partial class TradeProposal
{
    private CityId? _host;
    private ResourceType? _give;
    private ResourceType? _take;
    private int _giveAmount = 1;
    private int _takeAmount = 1;
    private TradeTerms? _seeded;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The deal's home city (the trader's).</summary>
    [Parameter, EditorRequired]
    public CityId Home { get; set; }

    /// <summary>The deal's host city when it is already known (a counter-offer); <see langword="null"/> to pick one.</summary>
    [Parameter]
    public CityId? FixedHost { get; set; }

    /// <summary>Terms to start from (the offer being countered); <see langword="null"/> for a new deal.</summary>
    [Parameter]
    public TradeTerms? Initial { get; set; }

    /// <summary>Whether the player is the host's side (the rows read "They give" and "You give").</summary>
    [Parameter]
    public bool AsHost { get; set; }

    /// <summary>The send button's text.</summary>
    [Parameter]
    public string SendText { get; set; } = "Propose";

    /// <summary>Why the rules would reject these terms to this host now; <see langword="null"/> when they would be accepted.</summary>
    [Parameter, EditorRequired]
    public Func<CityId, TradeTerms, RuleViolation?> Check { get; set; } = null!;

    /// <summary>Raised with the host and the terms when the player sends them.</summary>
    [Parameter]
    public EventCallback<(CityId Host, TradeTerms Terms)> Send { get; set; }

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Whether the player may act now (their turn, game in progress).</summary>
    [Parameter]
    public bool CanAct { get; set; }

    private ImmutableArray<(CityId Id, string Name, PlayerId Owner)> Targets => TradePreviewCalculator.Targets(View, Home);

    private CityId? Host => FixedHost ?? (_host is { } picked && Targets.Any(target => target.Id == picked) ? picked : null);

    private string HostName => Host is { } host ? CityName(host) : "the host";

    private ResourceType? Give => _give ?? First(MarketSide.Sell);

    private ResourceType? Take => _take ?? First(MarketSide.Buy);

    private TradeTerms Terms => new(
        Give is { } give ? [new ResourceAmount(give, _giveAmount)] : [],
        Take is { } take ? [new ResourceAmount(take, _takeAmount)] : []);

    private (MarketSide Side, string Label, ResourceType? Picked, int Amount)[] Rows =>
    [
        (MarketSide.Sell, AsHost ? "They give" : "Give", Give, _giveAmount),
        (MarketSide.Buy, AsHost ? "You give" : "Ask", Take, _takeAmount),
    ];

    private string? Problem => Host is { } host
        ? Check(host, Terms)?.Message
        : Targets.Length == 0 ? "There is no city to trade with." : "Pick the city to trade with.";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A counter-offer starts from the offer on the table, once per offer.
        if (Initial is { } initial && !ReferenceEquals(initial, _seeded))
        {
            _seeded = initial;
            (_give, _giveAmount) = initial.Gives.FirstOrDefault() is { } give ? ((ResourceType?)give.Resource, give.Amount) : (null, 1);
            (_take, _takeAmount) = initial.Receives.FirstOrDefault() is { } take ? ((ResourceType?)take.Resource, take.Amount) : (null, 1);
        }
    }

    private string TargetName(string name, PlayerId owner) => owner == View.Viewer ? name : $"{name} (Seat {Seats.Number(View, owner)})";

    private string CityName(CityId city) => View.Cities.FirstOrDefault(known => known.Id == city)?.Name
        ?? View.TradePartnerList.FirstOrDefault(partner => partner.Id == city)?.Name
        ?? "the host";

    // The tradable resources the home city sells (to give) or buys (to ask for), in its market's order.
    private IEnumerable<ResourceType> Choices(MarketSide side) =>
        TradePreviewCalculator.FreeMarket(View, Home).Where(order => order.Side == side).Select(static order => order.Resource);

    private ResourceType? First(MarketSide side) => Choices(side).Select(static resource => (ResourceType?)resource).FirstOrDefault();

    // How the host values the offer against what is asked, from its demand levels.
    private string? ValueText => (Host, Give, Take) is ({ }, { } give, { } take)
        ? $"{HostName} values what it gets at {Worth(give, _giveAmount)} against {Worth(take, _takeAmount)} it gives."
        : null;

    private string Worth(ResourceType resource, int amount) =>
        (HostValue(resource) * amount / 100.0).ToString("0.#", CultureInfo.InvariantCulture);

    private int HostValue(ResourceType resource) =>
        Host is { } host ? TradeCalculator.Value(Ruleset, TradePreviewCalculator.Demand(View, host), resource) : 0;

    private void Pick(MarketSide side, ResourceType resource)
    {
        if (side == MarketSide.Sell)
        {
            _give = resource;
        }
        else
        {
            _take = resource;
        }
    }

    private void SetAmount(MarketSide side, int amount)
    {
        if (side == MarketSide.Sell)
        {
            _giveAmount = amount;
        }
        else
        {
            _takeAmount = amount;
        }
    }

    // The least of the given resource the host values at least as much as what is asked.
    private void MakeFair()
    {
        if ((Give, Take) is ({ } give, { } take) && HostValue(give) > 0)
        {
            var asked = HostValue(take) * _takeAmount;
            _giveAmount = Math.Clamp((asked + HostValue(give) - 1) / HostValue(give), 1, Ruleset.Trade.MaxCap);
        }
    }

    private async Task SendAsync()
    {
        if (Host is { } host)
        {
            await Send.InvokeAsync((host, Terms));
        }
    }
}
