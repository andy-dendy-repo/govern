using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Govern.Client.Auth;

namespace Govern.ClientTests.Support;

/// <summary>Builds unsigned JWTs and token pairs shaped like the Api's (the client never validates signatures).</summary>
public static class TestTokens
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public static string AccessToken(string email, DateTimeOffset issuedAt, TimeSpan? lifetime = null, Guid? userId = null)
    {
        var header = Encode(new { alg = "HS256", typ = "JWT" });
        var payload = Encode(new Dictionary<string, object>
        {
            ["sub"] = (userId ?? Guid.CreateVersion7()).ToString(),
            ["email"] = email,
            ["jti"] = Guid.CreateVersion7().ToString(),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = (issuedAt + (lifetime ?? AccessTokenLifetime)).ToUnixTimeSeconds(),
        });
        return $"{header}.{payload}.signature";
    }

    public static TokenPair Pair(string email, DateTimeOffset issuedAt, string? refreshToken = null) =>
        new(AccessToken(email, issuedAt), issuedAt + AccessTokenLifetime, refreshToken ?? $"refresh-{Guid.CreateVersion7():N}", issuedAt.AddDays(14));

    private static string Encode(object value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
}
