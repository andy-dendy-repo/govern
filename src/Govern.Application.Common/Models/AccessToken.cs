namespace Govern.Application.Common.Models;

/// <summary>
/// A signed access token.
/// </summary>
/// <param name="Token">The encoded JWT.</param>
/// <param name="ExpiresAt">When the token expires.</param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
