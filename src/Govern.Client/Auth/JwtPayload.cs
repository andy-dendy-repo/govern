using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;

namespace Govern.Client.Auth;

/// <summary>
/// The claims of an access token, read from its payload. The signature is <b>not</b> validated: the client only uses the
/// claims for display and refresh timing, and the Api validates every token it receives.
/// </summary>
/// <param name="Claims">The string and number claims (e.g. <c>sub</c>, <c>email</c>).</param>
/// <param name="IssuedAt">The <c>iat</c> claim.</param>
/// <param name="ExpiresAt">The <c>exp</c> claim.</param>
public sealed record JwtPayload(IReadOnlyList<Claim> Claims, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt)
{
    /// <summary>How long the token is valid for, measured on the Api's clock.</summary>
    public TimeSpan Lifetime => ExpiresAt - IssuedAt;

    /// <summary>Reads the payload of a JWT.</summary>
    /// <param name="token">The compact JWT (<c>header.payload.signature</c>).</param>
    /// <returns>The payload, or <see langword="null"/> when the token is malformed or has no <c>iat</c> or <c>exp</c>.</returns>
    public static JwtPayload? TryRead(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadTime(root, "iat", out var issuedAt) || !TryReadTime(root, "exp", out var expiresAt))
            {
                return null;
            }

            var claims = root.EnumerateObject()
                .Where(static property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                .Select(static property => new Claim(property.Name, property.Value.ToString()))
                .ToList();
            return new JwtPayload(claims, issuedAt, expiresAt);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool TryReadTime(JsonElement root, string name, out DateTimeOffset value)
    {
        value = default;
        if (!root.TryGetProperty(name, out var claim) || claim.ValueKind != JsonValueKind.Number || !claim.TryGetInt64(out var seconds))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }
}
