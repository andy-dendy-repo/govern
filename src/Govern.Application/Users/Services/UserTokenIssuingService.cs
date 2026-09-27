using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Options;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Govern.Application.Users.Services;

/// <summary>
/// Issues access + refresh token pairs for sign-in and refresh. The refresh token is recorded on the
/// <see cref="User"/> aggregate; the caller saves the user.
/// </summary>
public sealed class UserTokenIssuingService(ITokenService tokenService, IOptions<JwtOptions> options)
{
    /// <summary>
    /// Issues a new token pair (sign-in). Beyond <see cref="JwtOptions.MaxActiveRefreshTokens"/>, the least recently
    /// used session is revoked.
    /// </summary>
    /// <param name="user">The tracked user with its active refresh tokens loaded; a new refresh token is added to it.</param>
    /// <param name="now">The current time of the request.</param>
    /// <returns>The token pair to return to the client.</returns>
    public AuthTokens Issue(User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var refreshToken = tokenService.CreateRefreshToken();
        var expiresAt = now + options.Value.RefreshTokenLifetime;
        user.IssueRefreshToken(refreshToken.TokenHash, expiresAt, now, options.Value.MaxActiveRefreshTokens);

        return new AuthTokens(tokenService.CreateAccessToken(user.Id, user.Email.Value), refreshToken.Token, expiresAt);
    }

    /// <summary>
    /// Replaces <paramref name="current"/> with a new token pair (refresh) and revokes it.
    /// </summary>
    /// <param name="user">The tracked owner of <paramref name="current"/>.</param>
    /// <param name="current">The active refresh token presented by the client.</param>
    /// <param name="now">The current time of the request.</param>
    /// <returns>The new token pair to return to the client.</returns>
    public AuthTokens Rotate(User user, RefreshToken current, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var refreshToken = tokenService.CreateRefreshToken();
        var expiresAt = now + options.Value.RefreshTokenLifetime;
        user.RotateRefreshToken(current, refreshToken.TokenHash, expiresAt, now);

        return new AuthTokens(tokenService.CreateAccessToken(user.Id, user.Email.Value), refreshToken.Token, expiresAt);
    }
}
