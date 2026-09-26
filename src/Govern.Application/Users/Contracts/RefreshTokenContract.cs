using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Application.Users.Services;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Exchanges a refresh token for a new token pair. Refresh tokens are single-use: the presented token is revoked.
/// Presenting an already revoked token is treated as theft and revokes all of the user's active refresh tokens.
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
/// Handles <see cref="RefreshTokenRequest"/> with rotation and reuse detection.
/// </summary>
internal sealed partial class RefreshTokenHandler(
    IApplicationDbContextFactory dbContextFactory,
    ITokenService tokenService,
    UserTokenIssuingService tokenIssuing,
    TimeProvider timeProvider,
    ILogger<RefreshTokenHandler> logger) : IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>
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

        if (presented.RevokedAt is not null)
        {
            user.RevokeAllRefreshTokens(now);
            await db.SaveChangesAsync(cancellationToken);
            Log.RefreshTokenReused(logger, user.Id);
            return UserErrors.InvalidRefreshToken;
        }

        if (!presented.IsActive(now))
        {
            return UserErrors.InvalidRefreshToken;
        }

        var tokens = tokenIssuing.Rotate(user, presented);
        await db.SaveChangesAsync(cancellationToken);

        return RefreshTokenResponse.From(tokens);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Revoked refresh token reused for user {UserId}; all active refresh tokens revoked")]
        public static partial void RefreshTokenReused(ILogger logger, Guid userId);
    }
}
