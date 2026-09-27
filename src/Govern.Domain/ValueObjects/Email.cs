using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Govern.Domain.ValueObjects;

/// <summary>
/// A normalised (trimmed, lower-case, Unicode NFC) email address. Two emails that differ only in case, surrounding
/// whitespace or Unicode composition are equal, so lookups and uniqueness checks can't skip normalisation.
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
    /// Whether <paramref name="value"/> is an acceptable address. After trimming it must be well-formed UTF-16 with no
    /// whitespace, control, format, private-use, unassigned or other invisible (default-ignorable) characters; after
    /// normalisation it must be at most <see cref="MaxLength"/> characters with exactly one <c>@</c> that is neither
    /// first nor last. Visible homoglyphs (Cyrillic <c>а</c>, fullwidth letters) are not rejected.
    /// </summary>
    /// <param name="value">The email as entered.</param>
    /// <returns><see langword="true"/> when <see cref="Create"/> would succeed.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) => TryNormalize(value, out _);

    /// <summary>
    /// Creates an email from untrusted input.
    /// </summary>
    /// <param name="value">The email as entered.</param>
    /// <param name="email">The normalised email when valid; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is valid.</returns>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Email? email)
    {
        email = TryNormalize(value, out var normalized) ? new Email(normalized) : null;
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

    private static bool TryNormalize([NotNullWhen(true)] string? value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !HasOnlyAllowedCharacters(trimmed))
        {
            return false;
        }

        // Only safe after the scan above: Normalize throws on lone surrogates. Lower-casing an NFC string can leave it
        // un-normalised (J + caron has no precomposed capital, but ǰ exists), so decompose, lower-case, then compose.
        var candidate = trimmed.Normalize(NormalizationForm.FormD).ToLowerInvariant().Normalize(NormalizationForm.FormC);
        if (candidate.Length > MaxLength || !HasSingleInnerAt(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static bool HasOnlyAllowedCharacters(string value)
    {
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done || IsDisallowed(rune))
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }

    private static bool IsDisallowed(Rune rune) =>
        Rune.IsWhiteSpace(rune)
        || IsInvisibleNonFormat(rune)
        || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned;

    // Unicode Default_Ignorable_Code_Point characters that are not in the Format category: combining grapheme joiner,
    // Hangul fillers, Khmer inherent vowels, Mongolian variation selectors and the variation selectors.
    private static bool IsInvisibleNonFormat(Rune rune) => rune.Value is 0x034F
        or (>= 0x115F and <= 0x1160)
        or (>= 0x17B4 and <= 0x17B5)
        or (>= 0x180B and <= 0x180F)
        or 0x3164
        or (>= 0xFE00 and <= 0xFE0F)
        or 0xFFA0
        or (>= 0xE0100 and <= 0xE01EF);

    private static bool HasSingleInnerAt(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < value.Length - 1 && at == value.LastIndexOf('@');
    }
}
