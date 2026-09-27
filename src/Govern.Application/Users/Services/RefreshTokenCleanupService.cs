using Govern.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Services;

/// <summary>
/// Deletes a user's expired refresh tokens on sign-in and refresh, so the <c>refresh_tokens</c> table stays bounded
/// without a background job. Revoked but unexpired tokens are kept: reuse detection needs them until they expire.
/// </summary>
public static class RefreshTokenCleanupService
{
    /// <summary>
    /// Deletes the expired refresh tokens of <paramref name="userId"/> in one statement (bypasses the change tracker).
    /// Pass the request's single <paramref name="now"/> so the domain and the delete agree on which tokens expired.
    /// </summary>
    /// <param name="db">The unit of work of the calling handler.</param>
    /// <param name="userId">The owner of the tokens.</param>
    /// <param name="now">The current time of the request.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The number of deleted tokens.</returns>
    public static Task<int> DeleteExpiredAsync(IApplicationDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.RefreshTokens
            .Where(token => token.UserId == userId && token.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
