namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The tokens returned by <c>POST /api/users/sign-in</c>.
/// </summary>
/// <param name="AccessToken">The JWT sent as the bearer token.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires, by the Api's clock.</param>
/// <param name="RefreshToken">The single-use refresh token.</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires, by the Api's clock.</param>
public sealed record SignInResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
