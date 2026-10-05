using System.Globalization;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The market of the player's own city (G17, G17b): what it sells and buys of each tradable resource, up to which cap, and what each
/// is worth there; changes are drafted here and sent one resource at a time (<see cref="SetMarketOrderCommand"/>).
/// </summary>
public partial class CityMarket
{
    private const int DefaultCap = 5;

    private static readonly (MarketSide? Side, string Label)[] Sides = [(null, "None"), (MarketSide.Sell, "Sell"), (MarketSide.Buy, "Buy")];

    private readonly Dictionary<ResourceType, (MarketSide? Side, int Cap)> _drafts = [];
    private CityId? _draftsFor;

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

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Drafts belong to one city; another city (or a new view of this one after a Set) starts from what is stored.
        if (_draftsFor != City.Id)
        {
            _drafts.Clear();
            _draftsFor = City.Id;
        }
    }

    private MarketOrder? Stored(ResourceType resource) => City.MarketList.FirstOrDefault(order => order.Resource == resource);

    private (MarketSide? Side, int Cap) Draft(ResourceType resource) =>
        _drafts.TryGetValue(resource, out var draft) ? draft : Stored(resource) is { } order ? (order.Side, order.Cap) : (null, DefaultCap);

    // Whether the draft differs from what is stored; without a side the cap doesn't matter.
    private bool Changed(ResourceType resource) =>
        Key(Draft(resource)) != Key(Stored(resource) is { } order ? (order.Side, order.Cap) : (null, 0));

    private static (MarketSide? Side, int Cap) Key((MarketSide? Side, int Cap) order) => order.Side is null ? (null, 0) : order;

    private void Pick(ResourceType resource, MarketSide? side) => _drafts[resource] = (side, Draft(resource).Cap);

    private void SetCap(ResourceType resource, int cap) => _drafts[resource] = (Draft(resource).Side, cap);

    private async Task SendAsync(ResourceType resource)
    {
        var (side, cap) = Draft(resource);
        _drafts.Remove(resource);
        await Command.InvokeAsync(new SetMarketOrderCommand(City.Id, resource, side, side is null ? 0 : cap));
    }

    // How much of the stored cap the city's agreed deals use.
    private string UsedText(ResourceType resource)
    {
        if (Stored(resource) is not { } order)
        {
            return string.Empty;
        }

        var deals = View.TradeDealList.Where(static deal => deal.IsAgreed).Select(static deal => (deal.Home, deal.Host, deal.Terms));
        var used = TradeCalculator.Committed(deals, City.Id, resource, order.Side);
        return $"{used} of {order.Cap} used";
    }

    // "Demand 120%, 1 = 2.4 Food": the level and the fair rate against the first tradable resource.
    private string RateText(ResourceType resource)
    {
        var percent = TradeCalculator.DemandPercent(City.DemandList, resource);
        var basis = Ruleset.TradableResources.First().Id;
        var rate = (double)TradeCalculator.Value(Ruleset, City.DemandList, resource) / TradeCalculator.Value(Ruleset, City.DemandList, basis);
        return resource == basis
            ? $"Demand {percent}%"
            : $"Demand {percent}%, 1 = {rate.ToString("0.#", CultureInfo.InvariantCulture)} {Ruleset.ResourceName(basis)}";
    }
}
