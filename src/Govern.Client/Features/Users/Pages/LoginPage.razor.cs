using Govern.Client.Auth;
using Govern.Client.Features.Users.Clients;
using Govern.Client.Features.Users.Helpers;
using Govern.Client.Features.Users.Models;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Users.Pages;

/// <summary>
/// Signs the user in, then goes to the local <see cref="ReturnPath"/> (plus the fragment the login URL carries, e.g. an invite
/// code) or home.
/// </summary>
public partial class LoginPage
{
    private readonly SignInRequest _form = new();
    private ApiError? _error;
    private bool _busy;

    /// <summary>The page to return to after signing in; only local paths are followed (<see cref="Auth.ReturnUrl.Sanitize"/>).</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnPath { get; set; }

    /// <summary>Set by the register page when the account was created but the automatic sign-in failed.</summary>
    [SupplyParameterFromQuery(Name = "registered")]
    public bool Registered { get; set; }

    [Inject]
    private UsersClient Users { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private string RegisterPath => AuthLinks.WithReturn("register", ReturnPath, Navigation);

    private async Task SubmitAsync()
    {
        _busy = true;
        _error = null;
        var result = await Users.SignInAsync(_form, CancellationToken.None);
        _busy = false;

        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        // Replace the login entry: Back should not return to the form, nor to a URL that carries an invite code.
        Navigation.NavigateTo(AuthLinks.Destination(ReturnPath, Navigation), replace: true);
    }
}
