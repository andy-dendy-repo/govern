namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The sign-in form and request body for <c>POST /api/users/sign-in</c>. Settable so it can bind to a form.
/// </summary>
public sealed class SignInRequest
{
    /// <summary>The account's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The password.</summary>
    public string Password { get; set; } = string.Empty;
}
