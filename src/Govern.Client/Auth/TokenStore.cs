using System.Text.Json;
using Govern.Client.Models;
using Microsoft.JSInterop;

namespace Govern.Client.Auth;

/// <summary>
/// Keeps the session tokens in the browser's <c>localStorage</c> (ADR 0002). This is the only code that touches
/// <c>localStorage</c> (<c>ClientSecurityTests</c>); everything else goes through <see cref="AuthSession"/>.
/// </summary>
/// <remarks>
/// Every call reads storage again (no cache), so all tabs of the app see the same, latest tokens.
/// </remarks>
public sealed class TokenStore(IJSRuntime js)
{
    /// <summary>The <c>localStorage</c> key holding the <see cref="StoredTokens"/> JSON.</summary>
    public const string Key = "govern.auth";

    /// <summary>Reads the stored tokens. A malformed entry is removed.</summary>
    /// <param name="cancellationToken">Cancels the JS call.</param>
    /// <returns>The tokens, or <see langword="null"/> when none are stored.</returns>
    public async Task<StoredTokens?> GetAsync(CancellationToken cancellationToken)
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, Key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        StoredTokens? tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<StoredTokens>(json, ApiJson.Options);
        }
        catch (JsonException)
        {
            tokens = null;
        }

        if (tokens is { AccessToken.Length: > 0, RefreshToken.Length: > 0 })
        {
            return tokens;
        }

        await ClearAsync(cancellationToken);
        return null;
    }

    /// <summary>Stores <paramref name="tokens"/>, replacing any previous session.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="cancellationToken">Cancels the JS call.</param>
    /// <returns>A task that completes when the tokens are stored.</returns>
    public async Task SetAsync(StoredTokens tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await js.InvokeVoidAsync("localStorage.setItem", cancellationToken, Key, JsonSerializer.Serialize(tokens, ApiJson.Options));
    }

    /// <summary>Removes the stored tokens.</summary>
    /// <param name="cancellationToken">Cancels the JS call.</param>
    /// <returns>A task that completes when the tokens are removed.</returns>
    public async Task ClearAsync(CancellationToken cancellationToken) =>
        await js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, Key);
}
