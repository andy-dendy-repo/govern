namespace Govern.Client.Auth;

/// <summary>
/// The tokens the Api returns from sign-in and refresh. A refresh rotates both: the old refresh token stops working.
/// </summary>
/// <param name="AccessToken">The JWT sent as the bearer token (short-lived).</param>
/// <param name="AccessTokenExpiresAt">When the access token expires, by the Api's clock.</param>
/// <param name="RefreshToken">The opaque, single-use refresh token.</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires, by the Api's clock.</param>
public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
