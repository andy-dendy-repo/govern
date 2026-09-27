using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Application.Users.Services;
using Govern.Domain.Entities;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Exchanges a refresh token for a new token pair. Refresh tokens are single-use: the presented token is revoked.
/// Presenting an already rotated token is treated as theft and revokes all of the user's active refresh tokens;
/// tokens revoked by sign-out or the session cap are simply rejected.
/// </summary>
/// <param name="RefreshToken">The raw refresh token from sign-in or a previous refresh.</param>
public sealed record RefreshTokenRequest(string RefreshToken) : IRequest<RefreshTokenRequest, RefreshTokenResponse>;

/// <summary>
/// The new tokens; the previous refresh token no longer works.
/// </summary>
/// <param name="AccessToken">Signed JWT access token.</param>
/// <param name="AccessTokenExpiresAt">Access token expiry.</param>
/// <param name="RefreshToken">New single-use refresh token.</param>
/// <param name="RefreshTokenExpiresAt">Refresh token expiry.</param>
public sealed record RefreshTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)
    : IResponse
{
    /// <summary>Maps an issued token pair to the response.</summary>
    /// <param name="tokens">The issued tokens.</param>
    /// <returns>The response.</returns>
    public static RefreshTokenResponse From(AuthTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new RefreshTokenResponse(tokens.AccessToken.Token, tokens.AccessToken.ExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
    }
}

/// <summary>
/// Validates <see cref="RefreshTokenRequest"/>.
/// </summary>
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public RefreshTokenValidator() => RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(256);
}

/// <summary>
/// Handles <see cref="RefreshTokenRequest"/> with rotation and reuse detection (<see cref="RefreshTokenReuseService"/>).
/// </summary>
internal sealed class RefreshTokenHandler(
    IApplicationDbContextFactory dbContextFactory,
    ITokenService tokenService,
    UserTokenIssuingService tokenIssuing,
    RefreshTokenReuseService tokenReuse,
    TimeProvider timeProvider) : IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<RefreshTokenResponse>> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var presented = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (presented is null)
        {
            return UserErrors.InvalidRefreshToken;
        }

        var user = await db.Users
            .Include(candidate => candidate.RefreshTokens.Where(token => token.RevokedAt == null && token.ExpiresAt > now))
            .SingleAsync(candidate => candidate.Id == presented.UserId, cancellationToken);

        if (RefreshTokenReuseService.IsReuse(presented))
        {
            await tokenReuse.RevokeAllAsync(db, user, now);
            return UserErrors.InvalidRefreshToken;
        }

        if (!presented.IsActive(now))
        {
            return UserErrors.InvalidRefreshToken;
        }

        return await RotateAsync(db, user, presented, now, cancellationToken);
    }

    // Deletes the user's expired tokens, then exchanges the presented token for a new pair.
    private async Task<Result<RefreshTokenResponse>> RotateAsync(
        IApplicationDbContext db,
        User user,
        RefreshToken presented,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await RefreshTokenCleanupService.DeleteExpiredAsync(db, user.Id, now, cancellationToken);
        var tokens = tokenIssuing.Rotate(user, presented, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent request rotated or revoked the same token first; only one rotation may win.
            return UserErrors.InvalidRefreshToken;
        }

        return RefreshTokenResponse.From(tokens);
    }
}
