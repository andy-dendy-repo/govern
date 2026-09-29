using Bunit.TestDoubles;
using System.Text.Json;
using Govern.Client;
using Govern.Client.Auth;
using Govern.Client.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Govern.ClientTests.Support;

/// <summary>
/// bUnit context with the real client registrations (<see cref="DependencyInjection.AddClient"/>) and a local Api URL, plus:
/// <list type="bullet">
/// <item><see cref="Api"/>: every typed and named <c>HttpClient</c> talks to this fake Api.</item>
/// <item><see cref="Storage"/>: browser storage, so the real <see cref="TokenStore"/> works.</item>
/// <item><see cref="Time"/>: a <see cref="FakeTimeProvider"/> replacing <see cref="TimeProvider"/>.</item>
/// <item><see cref="Authorization"/>: bUnit's authentication state for <c>AuthorizeView</c> (anonymous until <see cref="SignIn"/>).</item>
/// </list>
/// Radzen calls JavaScript on render, so JS interop runs in loose mode (unplanned calls return defaults).
/// </summary>
public abstract class ClientTestContext : BunitContext
{
    /// <summary>The Api base URL every test context is configured with.</summary>
    public const string ApiBaseUrl = "https://localhost:7294";

    /// <param name="fakeAuthorization">
    /// <see langword="true"/> (default) feeds <c>AuthorizeView</c> from bUnit's <see cref="Authorization"/>; <see langword="false"/>
    /// keeps the real <c>JwtAuthenticationStateProvider</c>, driven by <see cref="Storage"/>.
    /// </param>
    protected ClientTestContext(bool fakeAuthorization = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Storage = new FakeBrowserStorage(JSInterop);
        Services.AddClient(Configuration(("Api:BaseUrl", ApiBaseUrl)));
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<ICrossTabLock>(CrossTabLock);
        Services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => Api));
        Authorization = fakeAuthorization ? AddAuthorization() : null;
        if (!fakeAuthorization)
        {
            // bUnit registers a placeholder IAuthorizationService that fails the render; use the real one.
            Services.RemoveAll<IAuthorizationService>();
            Services.RemoveAll<IAuthorizationPolicyProvider>();
            Services.AddAuthorizationCore();
        }
    }

    protected FakeApiHandler Api { get; } = new();

    protected FakeBrowserStorage Storage { get; }

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    protected FakeCrossTabLock CrossTabLock { get; } = new();

    protected BunitAuthorizationContext? Authorization { get; }

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    /// <summary>Builds an in-memory configuration from key/value pairs.</summary>
    public static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(static value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    /// <summary>
    /// Stores a fresh session (due for renewal 14 minutes from now) and marks the user as signed in for <c>AuthorizeView</c>.
    /// </summary>
    protected StoredTokens SignIn(string email = "player@example.com")
    {
        var now = Time.GetUtcNow();
        var pair = TestTokens.Pair(email, now);
        var tokens = new StoredTokens(pair.AccessToken, pair.RefreshToken, pair.RefreshTokenExpiresAt, now + TimeSpan.FromMinutes(14));
        StoreTokens(tokens);
        Authorization?.SetAuthorized(email);
        return tokens;
    }

    /// <summary>Writes <paramref name="tokens"/> to browser storage the way <see cref="TokenStore"/> does.</summary>
    protected void StoreTokens(StoredTokens tokens) => Storage[TokenStore.Key] = JsonSerializer.Serialize(tokens, ApiJson.Options);

    /// <summary>The tokens currently in browser storage.</summary>
    protected StoredTokens? StoredTokens() =>
        Storage[TokenStore.Key] is { } json ? JsonSerializer.Deserialize<StoredTokens>(json, ApiJson.Options) : null;
}
