using System.Collections.Immutable;
using System.Globalization;
using Govern.Core.Engine;
using Govern.Core.Economy;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The proposal form of one of the player's trade routes (G17): one resource the home city gives and one it asks for each turn,
/// sent as a <see cref="Core.Actions.ProposeDealCommand"/>.
/// </summary>
public partial class TradeProposal
{
    private ResourceType? _give;
    private ResourceType? _take;
    private int _giveAmount = 1;
    private int _takeAmount = 1;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The route, one the player opened from their city.</summary>
    [Parameter, EditorRequired]
    public TradeRouteView Route { get; set; } = null!;

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Whether the player may act now (their turn, game in progress).</summary>
    [Parameter]
    public bool CanAct { get; set; }

    /// <summary>Raised with the proposal.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    private ResourceType? Give => _give ?? First(MarketSide.Sell);

    private ResourceType? Take => _take ?? First(MarketSide.Buy);

    private ImmutableArray<ResourceAmount> Gives => Give is { } give ? [new ResourceAmount(give, _giveAmount)] : [];

    private ImmutableArray<ResourceAmount> Receives => Take is { } take ? [new ResourceAmount(take, _takeAmount)] : [];

    private (MarketSide Side, string Label, ResourceType? Picked, int Amount)[] Rows =>
        [(MarketSide.Sell, "Give", Give, _giveAmount), (MarketSide.Buy, "Ask", Take, _takeAmount)];

    private RuleViolation? Problem => TradePreviewCalculator.CheckProposeDeal(Ruleset, View, Route.Id, Gives, Receives);

    // The tradable resources the home city sells (to give) or buys (to ask for), in ruleset order.
    private IEnumerable<ResourceType> Choices(MarketSide side) =>
        View.Cities.FirstOrDefault(city => city.Id == Route.Home)?.MarketList.Where(order => order.Side == side).Select(static order => order.Resource) ?? [];

    private ResourceType? First(MarketSide side) => Choices(side).Select(static resource => (ResourceType?)resource).FirstOrDefault();

    // How the host values the offer against what is asked, from its demand levels.
    private string? ValueText => (Give, Take) is ({ } give, { } take)
        ? $"{Route.HostName} values your offer at {Worth(give, _giveAmount)} against {Worth(take, _takeAmount)} asked."
        : null;

    private string Worth(ResourceType resource, int amount) =>
        (HostValue(resource) * amount / 100.0).ToString("0.#", CultureInfo.InvariantCulture);

    private int HostValue(ResourceType resource) => TradeCalculator.Value(Ruleset, Route.HostDemand ?? [], resource);

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
}
