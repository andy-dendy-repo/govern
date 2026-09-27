using Govern.Application.Common.Abstractions;
using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Govern.Application.Users.Services;

/// <summary>
/// Responds to a reused refresh token. A token that was already rotated (it has a successor) and is presented again,
/// to refresh or to sign out, means two parties hold the same chain: every active token of the user is revoked and a
/// Warning is logged (EventId 1001).
/// </summary>
public sealed partial class RefreshTokenReuseService(ILogger<RefreshTokenReuseService> logger)
{
    /// <summary>
    /// Whether presenting <paramref name="token"/> counts as reuse (theft): only rotated tokens have a successor.
    /// Tokens revoked by sign-out, the session cap or a previous theft response are not reuse.
    /// </summary>
    /// <param name="token">The presented token.</param>
    /// <returns><see langword="true"/> when the token was rotated.</returns>
    public static bool IsReuse(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.ReplacedByTokenId is not null;
    }

    /// <summary>
    /// Revokes every active refresh token of <paramref name="user"/> and logs the reuse. Security-critical, so it
    /// completes even if the caller disconnects (no request cancellation).
    /// </summary>
    /// <param name="db">The unit of work that tracks <paramref name="user"/>.</param>
    /// <param name="user">The tracked owner, with its active refresh tokens loaded.</param>
    /// <param name="now">The current time.</param>
    /// <returns>A task that completes when the tokens are revoked.</returns>
    public async Task RevokeAllAsync(IApplicationDbContext db, User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(user);

        user.RevokeAllRefreshTokens(now);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request revoked some of the tokens concurrently; they are revoked either way.
        }

        Log.RefreshTokenReused(logger, user.Id);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Rotated refresh token reused for user {UserId}; all active refresh tokens revoked")]
        public static partial void RefreshTokenReused(ILogger logger, Guid userId);
    }
}
