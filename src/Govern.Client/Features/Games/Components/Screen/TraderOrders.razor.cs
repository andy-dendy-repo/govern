using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>
/// A trader's orders (G17): open a route between one of the player's cities and another city's trade post
/// (<see cref="Core.Actions.OpenTradeRouteCommand"/>), or close the route it serves.
/// </summary>
public partial class TraderOrders
{
    private CityId? _home;
    private CityId? _host;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The game's ruleset.</summary>
    [Parameter, EditorRequired]
    public Ruleset Ruleset { get; set; } = null!;

    /// <summary>The player's trader.</summary>
    [Parameter, EditorRequired]
    public UnitView Unit { get; set; } = null!;

    /// <summary>Whether a command is in flight (the buttons are disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Raised with the command a button sends.</summary>
    [Parameter]
    public EventCallback<GameCommand> Command { get; set; }

    private bool MyTurn => View.CurrentPlayer == View.Viewer;

    // The player's cities with a trade post, the nearest to the trader first.
    private List<CityView> Homes => [.. View.Cities
        .Where(city => city.Owner == View.Viewer && city.TradePost is not null)
        .OrderBy(city => city.Position.DistanceTo(Unit.Position))
        .ThenBy(static city => city.Id.Value)];

    private CityId? Home => _home is { } picked && Homes.Any(city => city.Id == picked) ? picked : Homes.FirstOrDefault()?.Id;

    private CityId? Host => _host is { } picked && Hosts(Home).Any(city => city.Id == picked) ? picked : null;

    // Every other city whose trade post the player sees, theirs first.
    private IEnumerable<CityView> Hosts(CityId? home) => View.Cities
        .Where(city => city.TradePost is not null && city.Id != home)
        .OrderBy(city => city.Owner == View.Viewer ? 0 : 1)
        .ThenBy(city => city.Position.DistanceTo(Unit.Position));

    private (string Label, IEnumerable<CityView> Cities, CityId? Picked, Action<CityId> Pick)[] Pickers(CityId? home) =>
    [
        ("From", Homes, home, city => _home = city),
        ("To", Hosts(home), Host, city => _host = city),
    ];

    private string? Problem(CityId? home) => (home, Host) switch
    {
        (null, _) => $"None of your cities has a {Ruleset.BuildingName(Ruleset.Trade.Building)}.",
        (_, null) => "Pick the city to trade with: one with a trade post you can see.",
        ({ } from, { } to) => TradePreviewCalculator.CheckOpenTradeRoute(Ruleset, View, Unit.Id, from, to)?.Message,
    };
}
