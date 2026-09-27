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
/// </summary>
internal sealed class SignInHandler(
    IApplicationDbContextFactory dbContextFactory,
    IPasswordHashingService passwordHashing,
    UserTokenIssuingService tokenIssuing) : IRequestHandler<SignInRequest, SignInResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<SignInResponse>> HandleAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(request.Email, out var email))
        {
            passwordHashing.SimulateVerify(request.Password);
            return UserErrors.InvalidCredentials;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            passwordHashing.SimulateVerify(request.Password);
            return UserErrors.InvalidCredentials;
        }

        if (!passwordHashing.Verify(user.PasswordHash, request.Password))
        {
            return UserErrors.InvalidCredentials;
        }

        var tokens = tokenIssuing.Issue(user);
        await db.SaveChangesAsync(cancellationToken);

        return SignInResponse.From(tokens);
    }
}
