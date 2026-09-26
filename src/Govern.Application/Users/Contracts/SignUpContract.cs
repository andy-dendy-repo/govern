using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Creates a user account. A welcome email is sent by <c>UserSignedUpEventHandler</c> after the account is saved.
/// </summary>
/// <param name="Email">Email address; must be unique (case-insensitive).</param>
/// <param name="Password">Password, 8–128 characters.</param>
public sealed record SignUpRequest(string Email, string Password) : IRequest<SignUpRequest, SignUpResponse>;

/// <summary>
/// The created account.
/// </summary>
/// <param name="UserId">Identifier of the new user.</param>
public sealed record SignUpResponse(Guid UserId) : IResponse;

/// <summary>
/// Validates <see cref="SignUpRequest"/>.
/// </summary>
public sealed class SignUpValidator : AbstractValidator<SignUpRequest>
{
    /// <summary>Minimum password length.</summary>
    public const int PasswordMinLength = 8;

    /// <summary>Maximum password length (bounds hashing cost).</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Creates the validation rules.</summary>
    public SignUpValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().Length(PasswordMinLength, PasswordMaxLength);
    }
}

/// <summary>
/// Handles <see cref="SignUpRequest"/>: rejects duplicates (including concurrent ones) with <see cref="UserErrors.EmailTaken"/>.
/// </summary>
internal sealed class SignUpHandler(
    IApplicationDbContextFactory dbContextFactory,
    IPasswordHashingService passwordHashing,
    IDbExceptionClassificationService dbExceptions) : IRequestHandler<SignUpRequest, SignUpResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<SignUpResponse>> HandleAsync(SignUpRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return UserErrors.EmailTaken;
        }

        var user = User.SignUp(email, passwordHashing.Hash(request.Password));
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (dbExceptions.IsUniqueViolation(exception))
        {
            // A concurrent sign-up with the same email won the race for the unique index.
            return UserErrors.EmailTaken;
        }

        return new SignUpResponse(user.Id);
    }
}
