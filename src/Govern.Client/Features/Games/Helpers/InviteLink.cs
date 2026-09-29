using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds and reads invite links. The code travels only in the URL fragment (<c>/join#code</c>), which browsers never send
/// to a server, and then in the join request's body.
/// </summary>
public static partial class InviteLink
{
    /// <summary>The page that joins with the code in its fragment.</summary>
    public const string JoinPath = "join";

    /// <summary>Builds the link to share: <c>{base}join#{code}</c>.</summary>
    /// <param name="baseUri">The app's base URI (<c>NavigationManager.BaseUri</c>), ending with <c>/</c>.</param>
    /// <param name="code">The invite code.</param>
    /// <returns>The absolute link.</returns>
    public static string Build(string baseUri, string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseUri);
        ArgumentException.ThrowIfNullOrEmpty(code);
        return $"{baseUri}{JoinPath}#{code}";
    }

    /// <summary>
    /// Reads a code from what a player typed or pasted: a bare code, or a link (absolute or relative) to the join page with
    /// the code in its fragment. Surrounding whitespace is ignored.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="code">The code (22 base64url characters) when this returns <see langword="true"/>.</param>
    /// <returns>Whether <paramref name="input"/> holds a well-formed code.</returns>
    public static bool TryParse(string? input, [NotNullWhen(true)] out string? code)
    {
        code = null;
        var text = input?.Trim() ?? string.Empty;
        var hash = text.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            var target = text[..hash].TrimEnd('/');
            if (!target.EndsWith(JoinPath, StringComparison.Ordinal))
            {
                return false;
            }

            text = text[(hash + 1)..];
        }

        if (!CodeRegex().IsMatch(text))
        {
            return false;
        }

        code = text;
        return true;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex CodeRegex();
}
