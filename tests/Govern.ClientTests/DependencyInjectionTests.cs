using Govern.Client;
using Govern.Client.Options;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
}
