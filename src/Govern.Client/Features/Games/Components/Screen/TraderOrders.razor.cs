using Govern.Client.Features.Games.Services;
using Govern.Core.Economy;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>
/// A trader's line in the action bar (G17b): its home city, how its deal stands, and a button to that city's Market tab, where
/// its deals are made.
/// </summary>
public partial class TraderOrders
{
    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The player's trader.</summary>
    [Parameter, EditorRequired]
    public UnitView Unit { get; set; } = null!;

    /// <summary>Whether a command is in flight (the button is disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>The board's UI state (selection, open city tab).</summary>
    [CascadingParameter]
    public GameUiService Ui { get; set; } = null!;

    private CityView? Home => Unit.HomeCity is { } id ? View.Cities.FirstOrDefault(city => city.Id == id) : null;

    private TradeDealView? Deal => View.TradeDealList.FirstOrDefault(deal => deal.Trader == Unit.Id);

    private string Status(CityView home) => Deal switch
    {
        null => $"From {home.Name}, free for a deal.",
        { Status: TradeDealStatus.Proposed } deal => $"From {home.Name}; its offer to {deal.HostName} waits for an answer.",
        { Status: TradeDealStatus.Countered } deal => $"From {home.Name}; {deal.HostName} answered with other terms.",
        { Status: TradeDealStatus.Travelling } deal => $"From {home.Name}, on its way to {deal.HostName}.",
        { } deal => $"From {home.Name}, trading with {deal.HostName}.",
    };
}
