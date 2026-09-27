using System.Diagnostics.CodeAnalysis;

namespace Govern.Domain.ValueObjects;

/// <summary>
/// A normalised (trimmed, lower-case) email address. Two emails that differ only in case or surrounding whitespace
/// are equal, so lookups and uniqueness checks can't skip normalisation.
/// </summary>
public sealed record Email
{
    /// <summary>RFC 5321 maximum address length.</summary>
    public const int MaxLength = 320;

    private Email(string value) => Value = value;

    /// <summary>
    /// The normalised address.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Whether <paramref name="value"/> is an acceptable address: after trimming it is at most <see cref="MaxLength"/>
    /// characters and has exactly one <c>@</c> that is neither first nor last.
    /// </summary>
    /// <param name="value">The email as entered.</param>
    /// <returns><see langword="true"/> when <see cref="Create"/> would succeed.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength)
        {
            return false;
        }

        var at = trimmed.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < trimmed.Length - 1 && at == trimmed.LastIndexOf('@');
    }

    /// <summary>
    /// Creates an email from untrusted input.
    /// </summary>
    /// <param name="value">The email as entered.</param>
    /// <param name="email">The normalised email when valid; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is valid.</returns>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Email? email)
    {
        email = IsValid(value) ? new Email(value.Trim().ToLowerInvariant()) : null;
        return email is not null;
    }

    /// <summary>
    /// Creates an email from input that is already validated (or read from the database).
    /// </summary>
    /// <param name="value">The email.</param>
    /// <returns>The normalised email.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a valid email.</exception>
    public static Email Create(string value) =>
        TryCreate(value, out var email) ? email : throw new ArgumentException("The value is not a valid email address.", nameof(value));

    /// <summary>
    /// Returns the normalised address.
    /// </summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
