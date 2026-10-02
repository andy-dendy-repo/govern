using Govern.Client;
using Govern.Client.Auth;
using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Services;
using Govern.Client.Features.Users.Clients;
using Govern.Client.Services;
using Govern.Client.Options;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Govern.ClientTests;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("https://localhost:7294")]
    [InlineData("http://localhost:5153")]
    [InlineData("http://127.0.0.1:5153")]
    public void AddClient_ValidApiBaseUrl_BindsApiOptions(string baseUrl)
    {
        var services = new ServiceCollection();

        services.AddClient(ClientTestContext.Configuration(("Api:BaseUrl", baseUrl)));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ApiOptions>>().Value.BaseUrl.ShouldBe(new Uri(baseUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api")]
    [InlineData("http://api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("https://user:secret@api.example.com")]
    [InlineData("https://api.example.com/?x=1")]
    [InlineData("https://api.example.com/#x")]
    public void AddClient_MissingRelativeInsecureOrNonOriginApiBaseUrl_Throws(string? baseUrl)
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddClient(ClientTestContext.Configuration(("Api:BaseUrl", baseUrl))));

        exception.Message.ShouldContain("Api:BaseUrl");
    }

    [Fact]
    public void AddClient_DoesNotRegisterBareHttpClient()
    {
        var services = new ServiceCollection();

        services.AddClient(ClientTestContext.Configuration(("Api:BaseUrl", ClientTestContext.ApiBaseUrl)));

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(HttpClient),
            "components must call the Api through typed clients in Features/[Feature]/Clients");
    }

    [Fact]
    public void AddClient_ResolvesTypedClientsAndTheJwtAuthenticationState()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
        services.AddClient(ClientTestContext.Configuration(("Api:BaseUrl", ClientTestContext.ApiBaseUrl)));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<UsersClient>().ShouldNotBeNull();
        provider.GetRequiredService<GamesClient>().ShouldNotBeNull();
        provider.GetRequiredService<GameWatchService>().ShouldNotBeNull();
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ClipboardService>().ShouldNotBeNull();
        }

        provider.GetRequiredService<AuthenticationStateProvider>().ShouldBeSameAs(provider.GetRequiredService<JwtAuthenticationStateProvider>());
        provider.GetRequiredService<ICrossTabLock>().ShouldBeOfType<WebLocksCrossTabLock>();
    }

    [Fact]
    public async Task AddClient_ResolvesTheBoardInteropPerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
        services.AddClient(ClientTestContext.Configuration(("Api:BaseUrl", ClientTestContext.ApiBaseUrl)));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<Board3DInteropService>()
            .ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<Board3DInteropService>());
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
    }
}
