namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Hashes and verifies passwords with a slow, salted algorithm.
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>Hashes a password for storage.</summary>
    /// <param name="password">The plain password; never stored or logged.</param>
    /// <returns>The self-describing hash (algorithm, salt and iterations included).</returns>
    string Hash(string password);

    /// <summary>Checks a password against a stored hash in constant time.</summary>
    /// <param name="passwordHash">The stored hash.</param>
    /// <param name="password">The password to check.</param>
    /// <returns><see langword="true"/> when the password matches.</returns>
    bool Verify(string passwordHash, string password);
}
