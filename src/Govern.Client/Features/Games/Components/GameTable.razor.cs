using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Client.Models;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// A started game: the full-screen game view (the board, with the top strip, notices, selection card, attack forecast and log
/// drawer over it). It owns the screen's <see cref="GameUiService"/> (the selection, cascaded to the panels), follows the browser's
/// full screen, and sends the player's commands, one at a time,
/// based on the version of the view on screen. The game after a command (or, when the command was stale, the game read
/// again) is reported through <see cref="GameChanged"/>; the page owns the game.
/// </summary>
public partial class GameTable : IAsyncDisposable
{
    /// <summary>The Api's code for a command based on a view that has changed since.</summary>
    public const string StaleVersionCode = "game.stale_version";

    private readonly CancellationTokenSource _lifetime = new();
    private readonly GameUiService _ui = new();
    private BoardAnimationService _animation = null!;
    private ElementReference _screen;
    private ElementReference _strip;
    private bool _fullScreenSupported;
    private PlayerView? _view;
    private bool _focusScreen = true;
    private BuildingType? _placing;
    private bool _logOpen;
    private ApiError? _error;
    private bool _sending;
    private bool _stale;

    /// <summary>The game, started, with the caller's view.</summary>
    [Parameter, EditorRequired]
    public GetGameResponse Game { get; set; } = null!;

    /// <summary>Raised with the game after a command, or read again after a stale one.</summary>
    [Parameter]
    public EventCallback<GetGameResponse> GameChanged { get; set; }

    /// <summary>The game's title, shown in the top strip.</summary>
    [Parameter]
    public string Title { get; set; } = "Game";

    /// <summary>The live connection's state, shown as a badge in the top strip; <see langword="null"/> before it starts.</summary>
    [Parameter]
    public GameWatchUpdate? Connection { get; set; }

    [Inject]
    private GamesClient Games { get; set; } = null!;

    [Inject]
    private GameScreenInteropService Screen { get; set; } = null!;

    [Inject]
    private RulesetService Rulesets { get; set; } = null!;

    [Inject]
    private TimeProvider Time { get; set; } = null!;

    [Inject]
    private BoardInteropService Interop { get; set; } = null!;

    private Ruleset Ruleset => Rulesets.Ruleset;

    private PlayerView View => Game.View ?? throw new InvalidOperationException("The game table needs a started game with a view.");

    // The tile the attack forecast stays beside: the pending target, while the selected unit can fight it.
    private HexCoord? ForecastAt => _ui.Selection is { Unit: { } unit, Target: { } target } && View.UnitAt(target) is not null
        && GameEngine.CombatForecast(Ruleset, View, unit, target) is not null
            ? target
            : null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Screen.UnwatchAsync(_screen);
        _ui.Changed -= OnUiChanged;
        _animation.Changed -= OnAnimationChanged;
        _animation.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _ui.Changed += OnUiChanged;
        _ui.MarkLogSeen(Game.Log);
        _animation = new BoardAnimationService(Time);
        _animation.Changed += OnAnimationChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(View, _view))
        {
            // The new events play on the board while everything else already shows the new view.
            _ = _animation.PlayAsync(_view, View, Game.Log);
            _view = View;
            _ui.Refresh(Ruleset, View);
            if (_ui.LogOpen)
            {
                _ui.MarkLogSeen(Game.Log);
            }

            // A later view (a snapshot, not this table's own command) makes the last error or notice out of date.
            if (!_sending)
            {
                (_error, _stale) = (null, false);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Read once: the table can be disposed (and its token source with it) during the awaits.
            var cancellationToken = _lifetime.Token;
            _animation.ReducedMotion = await Interop.PrefersReducedMotionAsync(cancellationToken);
            _fullScreenSupported = !cancellationToken.IsCancellationRequested
                && await Screen.WatchAsync(_screen, _strip, OnFullScreenChanged, cancellationToken);
            if (_fullScreenSupported)
            {
                StateHasChanged();
            }
        }

        // Keep keyboard focus in the screen so Esc works: on arrival, and after a command, whose disabled buttons drop focus.
        if (_focusScreen)
        {
            _focusScreen = false;
            await _screen.FocusAsync(preventScroll: true);
        }
    }

    private async Task OnTileClickedAsync(HexCoord coord)
    {
        if (_sending)
        {
            return;
        }

        // Acting ends the animation, so the board shows exactly what the click is resolved against.
        _animation.Finish();
        var click = BoardClickResolver.Resolve(Ruleset, View, _ui.Selection, coord);

        _ui.Select(Ruleset, View, click.Next);
        if (click.Command is { } command)
        {
            await SendAsync(command);
        }
    }

    private async Task SendAsync(GameCommand command)
    {
        if (_sending)
        {
            return;
        }

        (_sending, _error, _stale) = (true, null, false);
        var cancellationToken = _lifetime.Token;
        try
        {
            var result = await Games.SendCommandAsync(Game.Id, View.Version, command, cancellationToken);
            if (result.IsSuccess)
            {
                await GameChanged.InvokeAsync(result.Value);
            }
            else if (result.Error.Code == StaleVersionCode)
            {
                await ReloadAsync(cancellationToken);
            }
            else
            {
                _error = result.Error;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The table was left while the command was in flight.
        }
        finally
        {
            _sending = false;
            _focusScreen = true;
        }
    }

    // The command was not applied: show the game as it is now, so the player can decide again.
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var result = await Games.GetAsync(Game.Id, cancellationToken);
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        _stale = true;
        await GameChanged.InvokeAsync(result.Value);
    }

    // Placing a building starts from a button inside the city panel's tabs, which keep Esc from reaching the screen, so the
    // screen takes focus back: Esc then cancels the placing.
    private void OnUiChanged()
    {
        if (_ui.Selection.Placing is { } placing && placing != _placing)
        {
            _focusScreen = true;
        }

        // Closing the log drawer removes the focused button with it; the screen takes focus back so Esc keeps working.
        _focusScreen |= _logOpen && !_ui.LogOpen;
        (_placing, _logOpen) = (_ui.Selection.Placing, _ui.LogOpen);
        StateHasChanged();
    }

    private Task ToggleFullScreenAsync() =>
        _ui.FullScreen ? Screen.ExitAsync(_lifetime.Token) : Screen.EnterAsync(_screen, _lifetime.Token);

    // The browser reports entering and leaving (Esc leaves without the button).
    private void OnFullScreenChanged(bool fullScreen) => _ = InvokeAsync(() => _ui.SetFullScreen(fullScreen));

    // Animation steps come from timers, outside the renderer's own events.
    private void OnAnimationChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            _ui.Escape(Ruleset, View);
        }
    }
}
