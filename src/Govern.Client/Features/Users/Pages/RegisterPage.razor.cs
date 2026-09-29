using Govern.Client.Features.Users.Clients;
using Govern.Client.Features.Users.Helpers;
using Govern.Client.Features.Users.Models;
using Govern.Client.Models;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Users.Pages;

/// <summary>
/// Creates an account and signs in with it, then goes to the local <see cref="ReturnPath"/> or home. When the account is
/// created but the sign-in fails (e.g. rate limited), it sends the user to login instead.
/// </summary>
public partial class RegisterPage
{
    private static readonly string PasswordLengthText =
        $"Use {RegisterForm.PasswordMinLength} to {RegisterForm.PasswordMaxLength} characters.";

    private readonly RegisterForm _form = new();
    private ApiError? _error;
    private bool _busy;

    /// <summary>The page to return to after registering; only local paths are followed.</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnPath { get; set; }

    [Inject]
    private UsersClient Users { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private string LoginPath => AuthLinks.WithReturn("login", ReturnPath, Navigation);

    private async Task SubmitAsync()
    {
        _busy = true;
        _error = null;
        var signUp = await Users.SignUpAsync(new SignUpRequest(_form.Email, _form.Password), CancellationToken.None);
        if (!signUp.IsSuccess)
        {
            _busy = false;
            _error = signUp.Error;
            return;
        }

        var signIn = await Users.SignInAsync(new SignInRequest { Email = _form.Email, Password = _form.Password }, CancellationToken.None);
        _busy = false;
        Navigation.NavigateTo(signIn.IsSuccess
            ? AuthLinks.Destination(ReturnPath, Navigation)
            : AuthLinks.WithReturn("login?registered=true", ReturnPath, Navigation));
    }
}
