using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Signs the caller out on every device by revoking all of their active refresh tokens. Access tokens stay valid
/// until they expire.
/// </summary>
public sealed record SignOutEverywhereRequest : IRequest<SignOutEverywhereRequest, SignOutEverywhereResponse>;

/// <summary>
/// All sessions are ended (empty; the endpoint returns 204).
/// </summary>
public sealed record SignOutEverywhereResponse : IResponse;

/// <summary>
/// Handles <see cref="SignOutEverywhereRequest"/> for the user in <see cref="ICurrentUserService"/>. A refresh that
/// rotates a token at the same moment makes the save fail on the concurrency token; the handler then reloads and
/// retries, so the newly issued token is revoked too.
/// </summary>
internal sealed class SignOutEverywhereHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRequestHandler<SignOutEverywhereRequest, SignOutEverywhereResponse>
{
    private const int MaxAttempts = 3;

    /// <inheritdoc />
    public async ValueTask<Result<SignOutEverywhereResponse>> HandleAsync(SignOutEverywhereRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RevokeAllAsync(userId);
                return new SignOutEverywhereResponse();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // A concurrent refresh rotated one of the tokens; reload and revoke again.
            }
        }
    }

    // Revocation is security-critical, so it completes even if the caller disconnects (no request cancellation).
    private async Task RevokeAllAsync(Guid userId)
    {
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var user = await db.Users
            .Include(candidate => candidate.RefreshTokens.Where(token => token.RevokedAt == null && token.ExpiresAt > now))
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, CancellationToken.None);

        if (user is null)
        {
            return;
        }

        user.RevokeAllRefreshTokens(now);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
