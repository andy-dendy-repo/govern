namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The register form. Sent as a <see cref="SignUpRequest"/>; <see cref="ConfirmPassword"/> stays in the browser.
/// </summary>
public sealed class RegisterForm
{
    /// <summary>The Api's minimum password length.</summary>
    public const int PasswordMinLength = 8;

    /// <summary>The Api's maximum password length.</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>The email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The password again, to catch typos.</summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
