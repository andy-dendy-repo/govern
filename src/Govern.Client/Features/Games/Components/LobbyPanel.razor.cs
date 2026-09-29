using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Models;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components;

/// <summary>
/// The lobby actions for one game. Start reports the started game through <see cref="Started"/>; a finished cancel or leave
/// raises <see cref="Closed"/>.
/// </summary>
public partial class LobbyPanel
{
    private ApiError? _error;
    private bool _busy;
    private LobbyAction? _confirming;

    /// <summary>The lobby, as the caller sees it.</summary>
    [Parameter, EditorRequired]
    public GetGameResponse Game { get; set; } = null!;

    /// <summary>Raised with the started game.</summary>
    [Parameter]
    public EventCallback<GetGameResponse> Started { get; set; }

    /// <summary>Raised after the caller cancelled or left the lobby.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    [Inject]
    private GamesClient Games { get; set; } = null!;

    private void Confirm(LobbyAction action)
    {
        _error = null;
        _confirming = action;
    }

    private async Task StartAsync()
    {
        _busy = true;
        _error = null;
        var result = await Games.StartAsync(Game.Id, CancellationToken.None);
        _busy = false;
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        await Started.InvokeAsync(result.Value);
    }

    private async Task ConfirmedAsync()
    {
        _busy = true;
        _error = null;
        var result = _confirming == LobbyAction.Cancel
            ? await Games.CancelAsync(Game.Id, CancellationToken.None)
            : await Games.LeaveAsync(Game.Id, CancellationToken.None);
        _busy = false;
        _confirming = null;
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        await Closed.InvokeAsync();
    }

    private enum LobbyAction
    {
        Cancel,
        Leave,
    }
}
