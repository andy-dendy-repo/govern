using Govern.Application.Common.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Govern.Infrastructure.Identity.Services;

/// <summary>
/// Password hashing with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2 with HMAC-SHA512,
/// random salt, versioned format), without the rest of Identity.
/// </summary>
internal sealed class PasswordHashingService : IPasswordHashingService
{
    // The default hasher ignores the user instance; a shared placeholder avoids passing nulls.
    private static readonly object PasswordOwner = new();
    private readonly PasswordHasher<object> _hasher = new();

    /// <inheritdoc />
    public string Hash(string password) => _hasher.HashPassword(PasswordOwner, password);

    /// <inheritdoc />
    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(PasswordOwner, passwordHash, password) is not PasswordVerificationResult.Failed;
}
