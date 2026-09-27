using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Application.Users.Services;
using Govern.Domain.ValueObjects;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Signs a user in with email and password and issues an access + refresh token pair.
/// </summary>
/// <param name="Email">Email address.</param>
/// <param name="Password">Password.</param>
public sealed record SignInRequest(string Email, string Password) : IRequest<SignInRequest, SignInResponse>;

/// <summary>
/// The issued tokens. Send the access token as <c>Authorization: Bearer</c>; exchange the refresh token at
/// <c>/api/users/refresh</c> before or after the access token expires.
/// </summary>
/// <param name="AccessToken">Signed JWT access token.</param>
/// <param name="AccessTokenExpiresAt">Access token expiry.</param>
/// <param name="RefreshToken">Single-use refresh token.</param>
/// <param name="RefreshTokenExpiresAt">Refresh token expiry.</param>
public sealed record SignInResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)
    : IResponse
{
    /// <summary>Maps an issued token pair to the response.</summary>
    /// <param name="tokens">The issued tokens.</param>
    /// <returns>The response.</returns>
    public static SignInResponse From(AuthTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new SignInResponse(tokens.AccessToken.Token, tokens.AccessToken.ExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
    }
}

/// <summary>
/// Validates <see cref="SignInRequest"/> (presence and size only; no password rules are revealed).
/// </summary>
public sealed class SignInValidator : AbstractValidator<SignInRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public SignInValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(Email.MaxLength);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(SignUpValidator.PasswordMaxLength);
    }
}

/// <summary>
/// Handles <see cref="SignInRequest"/>. Malformed or unknown emails and wrong passwords return the same
/// <see cref="UserErrors.InvalidCredentials"/> and take comparable time (a hash is always verified).
/// Each attempt for a valid email is reserved with <see cref="ISignInThrottlingService"/> before the password is checked;
/// past <see cref="Common.Options.SignInThrottlingOptions.MaxFailedAttempts"/> within the window, sign-in returns
/// <see cref="UserErrors.TooManySignInAttempts"/> without checking the password. Success resets the count.
/// A successful sign-in deletes the user's expired refresh tokens and applies the active-session cap. Evicting a session
/// races with refreshes and sign-outs of that token (concurrency token on <c>revoked_at</c>), so issuing is retried
/// with fresh state; the password is verified only once.
/// </summary>
internal sealed class SignInHandler(
    IApplicationDbContextFactory dbContextFactory,
    IPasswordHashingService passwordHashing,
    ISignInThrottlingService signInThrottling,
    UserTokenIssuingService tokenIssuing,
    TimeProvider timeProvider) : IRequestHandler<SignInRequest, SignInResponse>
{
    private const int MaxAttempts = 3;

    /// <inheritdoc />
    public async ValueTask<Result<SignInResponse>> HandleAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(request.Email, out var email))
        {
            passwordHashing.SimulateVerify(request.Password);
            return UserErrors.InvalidCredentials;
        }

        if (!signInThrottling.TryBeginAttempt(email))
        {
            return UserErrors.TooManySignInAttempts;
        }

        if (await FindVerifiedUserIdAsync(email, request.Password, cancellationToken) is not { } userId)
        {
            return UserErrors.InvalidCredentials;
        }

        signInThrottling.Reset(email);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return SignInResponse.From(await IssueAsync(userId, cancellationToken));
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // The evicted session was refreshed or revoked concurrently; reload and evict the next one.
            }
        }
    }

    // Returns the user id when the password matches; always verifies a hash so timing doesn't reveal accounts.
    private async Task<Guid?> FindVerifiedUserIdAsync(Email email, string password, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Email == email)
            .Select(candidate => new { candidate.Id, candidate.PasswordHash })
            .FirstOrDefaultAsync(cancellationToken);

        if (credentials is null)
        {
            passwordHashing.SimulateVerify(password);
            return null;
        }

        return passwordHashing.Verify(credentials.PasswordHash, password) ? credentials.Id : null;
    }

    private async Task<AuthTokens> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.RefreshTokens.Where(token => token.RevokedAt == null && token.ExpiresAt > now))
            .SingleAsync(candidate => candidate.Id == userId, cancellationToken);

        await RefreshTokenCleanupService.DeleteExpiredAsync(db, userId, now, cancellationToken);
        var tokens = tokenIssuing.Issue(user, now);
        await db.SaveChangesAsync(cancellationToken);
        return tokens;
    }
}
