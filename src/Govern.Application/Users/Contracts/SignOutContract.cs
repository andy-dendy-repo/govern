using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Application.Users.Services;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Signs the caller out on one device by revoking that device's refresh token. The access token stays valid until it
/// expires. Unknown, already revoked and other users' tokens succeed too, so the response reveals nothing. Signing out
/// with an already rotated token is reuse (theft) and revokes all of the caller's sessions, as a refresh would.
/// </summary>
/// <param name="RefreshToken">The raw refresh token of the session to end.</param>
public sealed record SignOutRequest(string RefreshToken) : IRequest<SignOutRequest, SignOutResponse>;

/// <summary>
/// The session is ended (empty; the endpoint returns 204).
/// </summary>
public sealed record SignOutResponse : IResponse;

/// <summary>
/// Validates <see cref="SignOutRequest"/>.
/// </summary>
public sealed class SignOutValidator : AbstractValidator<SignOutRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public SignOutValidator() => RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(256);
}

/// <summary>
/// Handles <see cref="SignOutRequest"/> for the user in <see cref="ICurrentUserService"/>. A refresh that rotates the
/// same token at the same moment makes the save fail on the concurrency token; the handler then re-evaluates the token,
/// which is now rotated and therefore handled as reuse.
/// </summary>
internal sealed class SignOutHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    ITokenService tokenService,
    RefreshTokenReuseService tokenReuse,
    TimeProvider timeProvider) : IRequestHandler<SignOutRequest, SignOutResponse>
{
    private const int MaxAttempts = 3;

    /// <inheritdoc />
    public async ValueTask<Result<SignOutResponse>> HandleAsync(SignOutRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RevokeAsync(userId, hash);
                return new SignOutResponse();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // A concurrent refresh rotated (or a sign-out revoked) the token; reload and decide again.
            }
        }
    }

    // Revocation is security-critical, so it completes even if the caller disconnects (no request cancellation).
    private async Task RevokeAsync(Guid userId, string hash)
    {
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);

        // Loads the presented token in any state, plus the active tokens a reuse response has to revoke.
        var user = await db.Users
            .Include(candidate => candidate.RefreshTokens.Where(token =>
                token.TokenHash == hash || (token.RevokedAt == null && token.ExpiresAt > now)))
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, CancellationToken.None);

        if (user?.RefreshTokens.SingleOrDefault(token => token.TokenHash == hash) is not { } presented)
        {
            return;
        }

        if (RefreshTokenReuseService.IsReuse(presented))
        {
            await tokenReuse.RevokeAllAsync(db, user, now);
            return;
        }

        user.RevokeRefreshToken(presented, now);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
