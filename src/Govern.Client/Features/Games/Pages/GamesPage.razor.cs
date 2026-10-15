using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Govern.Client.Features.Games.Pages;

/// <summary>
/// The caller's games, most recent activity first and paged by the Api, with buttons to create a game or join one.
/// </summary>
public partial class GamesPage
{
    private const int PageSize = 20;

    // Null until the first load: the grid calls LoadData on its first render only while Data is null.
    private IReadOnlyList<MyGameItem>? _items;
    private int _total;
    private bool _loading;
    private bool _creating;
    private string _map = MapPoolService.Random;
    private int _seats = 2;
    private ApiError? _error;

    [Inject]
    private GamesClient Games { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private MapPoolService Maps { get; set; } = null!;

    [Inject]
    private RulesetService Rulesets { get; set; } = null!;

    // From the ruleset's fewest to its most players.
    private IReadOnlyList<int> SeatChoices => field ??=
        [.. Enumerable.Range(Rulesets.Ruleset.Setup.MinPlayers, Rulesets.Ruleset.Setup.MaxPlayers - Rulesets.Ruleset.Setup.MinPlayers + 1)];

    // A map made for another number of players is no longer a choice, so the picker goes back to random.
    private void SeatsChanged(int seats)
    {
        _seats = seats;
        if (Maps.OptionsFor(seats).All(option => option.Value != _map))
        {
            _map = MapPoolService.Random;
        }
    }

    private async Task LoadAsync(LoadDataArgs args)
    {
        _loading = true;
        var result = await Games.ListAsync(((args.Skip ?? 0) / PageSize) + 1, PageSize, CancellationToken.None);
        _loading = false;
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        _error = null;
        _items = result.Value.Items;
        _total = result.Value.Total;
    }

    private async Task CreateAsync()
    {
        _creating = true;
        _error = null;
        var result = await Games.CreateAsync(_map, _seats, CancellationToken.None);
        _creating = false;
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        Navigation.NavigateTo($"games/{result.Value.Id}");
    }

    private static string PhaseText(GamePhase phase) => phase switch
    {
        GamePhase.Lobby => "Lobby",
        GamePhase.InProgress => "In progress",
        _ => "Finished",
    };
}
