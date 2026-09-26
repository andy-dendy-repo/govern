using Govern.Utils.Results;

namespace Govern.Application.Users.Models;

/// <summary>
/// Errors returned by the Users use cases. Messages are safe for clients and never reveal whether an account exists.
/// </summary>
public static class UserErrors
{
    /// <summary>Sign-up with an email that is already registered.</summary>
    public static readonly Error EmailTaken = Error.Conflict("user.email_taken", "An account with this email already exists.");

    /// <summary>Unknown email or wrong password; deliberately indistinguishable.</summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized("user.invalid_credentials", "Invalid email or password.");

    /// <summary>Unknown, expired or revoked refresh token.</summary>
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("user.invalid_refresh_token", "The refresh token is invalid or expired.");

    /// <summary>The request is not authenticated.</summary>
    public static readonly Error NotAuthenticated = Error.Unauthorized("user.not_authenticated", "Authentication is required.");

    /// <summary>The authenticated user no longer exists.</summary>
    public static readonly Error NotFound = Error.NotFound("user.not_found", "The user was not found.");
}
