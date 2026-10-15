using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Pages;

/// <summary>
/// One game, kept live: reads it, then follows its stream (<see cref="GameWatchService"/>) until the page is left. Shows the
/// lobby (players, invite, start/cancel/leave) or, once started, the board (<see cref="Components.GameTable"/>). The game
/// is replaced by every stream snapshot and by the answer to each of the player's commands, whichever arrives last.
/// </summary>
public partial class GamePage : IAsyncDisposable
{
    // Cancelled when the page is disposed, so work still in flight then (the first read) never starts a watch.
    private readonly CancellationTokenSource _lifetime = new();
    private Guid _loadedId;
    private GetGameResponse? _game;
    private GameWatchUpdate? _watch;
    private ApiError? _error;
    private bool _notFound;
    private CancellationTokenSource? _watchCancellation;
    private Task? _watching;

    /// <summary>The game's id, from the route.</summary>
    [Parameter]
    public Guid GameId { get; set; }

    [Inject]
    private GamesClient Games { get; set; } = null!;

    [Inject]
    private GameWatchService Watcher { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private MapPoolService Maps { get; set; } = null!;

    // Whether the board is on screen (a started or finished game), which fills the window with the game screen.
    private bool OnBoard => _game is { Phase: not GamePhase.Lobby, View: not null } && !_notFound && _watch?.Status != GameWatchStatus.Gone;

    private string Title => _game?.Phase switch
    {
        GamePhase.Lobby => "Lobby",
        GamePhase.InProgress => "Game in progress",
        GamePhase.Finished => "Game over",
        _ => "Game",
    };

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (GameId == _loadedId)
        {
            return;
        }

        var gameId = _loadedId = GameId;
        await StopWatchingAsync();
        (_game, _watch, _error, _notFound) = (null, null, null, false);

        ApiResult<GetGameResponse> result;
        try
        {
            result = await Games.GetAsync(gameId, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }

        if (gameId != _loadedId || _lifetime.IsCancellationRequested)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            _notFound = result.Error.Status == 404;
            _error = _notFound ? null : result.Error;
            return;
        }

        _game = result.Value;
        _watchCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _watching = WatchAsync(gameId, _watchCancellation.Token);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        await StopWatchingAsync();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task WatchAsync(Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var update in Watcher.WatchAsync(gameId, cancellationToken))
            {
                _watch = update;
                _game = update.Game ?? _game;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page moved to another game or was left.
        }
    }

    // Takes the watch out of the fields first, so a second caller (a new game id, or disposal, while this one waits) has
    // nothing left to stop.
    private async Task StopWatchingAsync()
    {
        var (cancellation, watching) = (_watchCancellation, _watching);
        (_watchCancellation, _watching) = (null, null);
        if (cancellation is null)
        {
            return;
        }

        await cancellation.CancelAsync();
        if (watching is not null)
        {
            await watching;
        }

        cancellation.Dispose();
    }

    private void OnStarted(GetGameResponse game) => OnGameChanged(game);

    // An answer for a game the page has since left (the route changed while a command was in flight) is dropped, and so is
    // one older than the view already shown: in a simultaneous game a pushed update can land before a command's answer.
    private void OnGameChanged(GetGameResponse game)
    {
        if (game.Id == _loadedId && NewestLog(game) >= NewestLog(_game))
        {
            _game = game;
        }
    }

    private static int NewestLog(GetGameResponse? game) =>
        game?.Log is { Count: > 0 } log ? log.Max(static item => item.Sequence) : 0;

    private void OnClosed() => Navigation.NavigateTo("games");
}
