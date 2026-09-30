using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Pages;

/// <summary>
/// Joins a game from an invite link (<c>/join#code</c>) or a pasted link or code. The code from the fragment is removed from
/// the address bar and the Back history at once and only fills in the form: joining takes a click, so a link another site
/// opens can't put the player into a game. The code is sent only in the request body.
/// </summary>
public partial class JoinPage
{
    private readonly JoinGameRequest _form = new();
    private ApiError? _error;
    private bool _joining;
    private bool _invalidLink;

    [Inject]
    private GamesClient Games { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        var fragment = new Uri(Navigation.Uri).Fragment;
        if (fragment.Length <= 1)
        {
            return;
        }

        // Replace the history entry first, so the code doesn't stay in the address bar or in Back.
        Navigation.NavigateTo(InviteLink.JoinPath, replace: true);
        if (InviteLink.TryParse(fragment[1..], out var code))
        {
            _form.InviteCode = code;
        }
        else
        {
            _invalidLink = true;
        }
    }

    private bool IsLinkOrCode() => string.IsNullOrWhiteSpace(_form.InviteCode) || InviteLink.TryParse(_form.InviteCode, out _);

    private async Task SubmitAsync()
    {
        _invalidLink = false;
        if (InviteLink.TryParse(_form.InviteCode, out var code))
        {
            await JoinAsync(code);
        }
    }

    private async Task JoinAsync(string code)
    {
        _joining = true;
        _error = null;
        var result = await Games.JoinAsync(code, CancellationToken.None);
        _joining = false;
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        Navigation.NavigateTo($"games/{result.Value.GameId}", replace: true);
    }
}
