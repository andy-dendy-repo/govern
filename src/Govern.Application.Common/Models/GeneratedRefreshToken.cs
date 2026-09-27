namespace Govern.Application.Common.Models;

/// <summary>
/// A newly generated refresh token.
/// </summary>
/// <param name="Token">The raw token, returned to the client once and never stored.</param>
/// <param name="TokenHash">The hash that is stored.</param>
public sealed record GeneratedRefreshToken(string Token, string TokenHash);
