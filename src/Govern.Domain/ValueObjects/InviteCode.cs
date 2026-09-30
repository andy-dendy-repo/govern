using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Govern.Domain.ValueObjects;

/// <summary>
/// The secret part of a game's invite link: 128 random bits as unpadded base64url. Whoever holds it can take a free seat,
/// so it is unguessable and case-sensitive, and it is never logged or put in a URL the server sees.
/// </summary>
public sealed record InviteCode
{
    /// <summary>Length of every code: 16 bytes as unpadded base64url.</summary>
    public const int MaxLength = 22;

    private const int ByteCount = 16;

    private static readonly SearchValues<char> Base64UrlCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    private InviteCode(string value) => Value = value;

    /// <summary>
    /// The code as shown to users.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Generates a new code from a cryptographically secure random number generator.
    /// </summary>
    /// <returns>The new code.</returns>
    public static InviteCode New() => new(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteCount)));

    /// <summary>
    /// Whether <paramref name="value"/> has the shape of a code: exactly <see cref="MaxLength"/> base64url characters
    /// (<c>A–Z</c>, <c>a–z</c>, <c>0–9</c>, <c>-</c>, <c>_</c>), with no whitespace or padding. Codes are not normalised.
    /// </summary>
    /// <param name="value">The code as entered.</param>
    /// <returns><see langword="true"/> when <see cref="Create"/> would succeed.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is { Length: MaxLength } && !value.AsSpan().ContainsAnyExcept(Base64UrlCharacters);

    /// <summary>
    /// Creates a code from untrusted input.
    /// </summary>
    /// <param name="value">The code as entered.</param>
    /// <param name="code">The code when valid; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is valid.</returns>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out InviteCode? code)
    {
        code = IsValid(value) ? new InviteCode(value) : null;
        return code is not null;
    }

    /// <summary>
    /// Creates a code from input that is already validated (or read from the database).
    /// </summary>
    /// <param name="value">The code.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a valid code.</exception>
    public static InviteCode Create(string value) =>
        TryCreate(value, out var code) ? code : throw new ArgumentException("The value is not a valid invite code.", nameof(value));

    /// <summary>
    /// Returns the code.
    /// </summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
