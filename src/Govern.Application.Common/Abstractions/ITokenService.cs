using Govern.Application.Common.Models;

namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Issues signed access tokens (JWT) and opaque refresh tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>Creates a signed, short-lived access token for the user.</summary>
    /// <param name="userId">The user id (the token subject).</param>
    /// <param name="email">The user's email (a token claim).</param>
    /// <returns>The token and its expiry.</returns>
    AccessToken CreateAccessToken(Guid userId, string email);

    /// <summary>Creates a new cryptographically random refresh token.</summary>
    /// <returns>The raw token for the client and its hash for storage.</returns>
    GeneratedRefreshToken CreateRefreshToken();

    /// <summary>Hashes a raw refresh token the same way <see cref="CreateRefreshToken"/> does, for lookups.</summary>
    /// <param name="refreshToken">The raw token presented by the client.</param>
    /// <returns>The hash.</returns>
    string HashRefreshToken(string refreshToken);
}
