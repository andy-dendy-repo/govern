using Govern.Application.Common.Models;

namespace Govern.Application.Users.Models;

/// <summary>
/// A freshly issued token pair.
/// </summary>
/// <param name="AccessToken">The signed access token and its expiry.</param>
/// <param name="RefreshToken">The raw refresh token (returned once; only its hash is stored).</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires.</param>
public sealed record AuthTokens(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
