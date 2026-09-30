namespace Govern.Client.Auth;

/// <summary>
/// The session as kept in browser storage by <see cref="TokenStore"/>, shared by all tabs.
/// </summary>
/// <param name="AccessToken">The current access token (JWT).</param>
/// <param name="RefreshToken">The current refresh token.</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires; after that the user is signed out.</param>
/// <param name="RenewAt">
/// When to refresh ahead of the access token's expiry, by the local clock: the time the tokens arrived plus the token's own
/// lifetime (<c>exp</c> − <c>iat</c>) minus a margin, so a skewed local clock does not matter.
/// </param>
public sealed record StoredTokens(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, DateTimeOffset RenewAt);
