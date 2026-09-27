using Govern.Client;
using Microsoft.Extensions.Configuration;

namespace Govern.ClientTests.Support;

/// <summary>
/// bUnit context with the real client registrations (<see cref="DependencyInjection.AddClient"/>) and a local Api URL.
/// Radzen calls JavaScript on render, so JS interop runs in loose mode (unplanned calls return defaults).
/// </summary>
public abstract class ClientTestContext : BunitContext
{
    /// <summary>The Api base URL every test context is configured with.</summary>
    public const string ApiBaseUrl = "https://localhost:7294";

    protected ClientTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddClient(Configuration(("Api:BaseUrl", ApiBaseUrl)));
    }

    /// <summary>Builds an in-memory configuration from key/value pairs.</summary>
    public static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(static value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
