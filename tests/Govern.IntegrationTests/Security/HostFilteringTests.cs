using System.Net;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Govern.IntegrationTests.Security;

public class HostFilteringTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_HostNotAllowed_Returns400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, "localhost");

        var response = await client.GetAsync("/api/users/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code: null, Ct);
    }

    [Fact]
    public async Task GetReady_HostNotAllowed_Returns400()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, "localhost");

        var response = await client.GetAsync("/health/ready", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLive_HostNotAllowed_IsExemptAndReturns200()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, "169.254.130.4");

        var response = await client.GetAsync("/health/live", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("api.govern.test")]
    [InlineData("API.Govern.Test")]
    [InlineData("eu.apps.govern.test")]
    public async Task Get_HostAllowed_ReachesTheEndpoint(string host)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, host);

        var response = await client.GetAsync("/api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AllowedHostWithPort_ReachesTheEndpoint()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, "api.govern.test:8443");

        var response = await client.GetAsync("/api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://api.govern.test")]
    [InlineData("api.govern.test:443")]
    [InlineData("api.govern.test/")]
    [InlineData("*.com")]
    [InlineData("*.")]
    [InlineData("api.*.govern.test")]
    [InlineData("api.govern.test.")]
    public void Startup_InvalidAllowedHost_FailsValidation(string host)
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("HostFiltering:AllowedHosts:0", host));

        Should.Throw<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Startup_EmptyAllowListOutsideDevelopment_FailsValidation()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseEnvironment("Production"));

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());
        exception.Message.ShouldContain("HostFiltering:AllowedHosts is required");
    }

    [Theory]
    [InlineData("apps.govern.test")]
    [InlineData("evilgovern.test")]
    [InlineData("api.govern.test.evil.example")]
    public async Task Get_HostOnlyResemblingAnAllowedOne_Returns400(string host)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory, host);

        var response = await client.GetAsync("/api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private WebApplicationFactory<Program> CreateFactory() => Fixture.Factory.WithWebHostBuilder(static builder => builder
        .UseSetting("HostFiltering:AllowedHosts:0", "api.govern.test")
        .UseSetting("HostFiltering:AllowedHosts:1", "*.apps.govern.test"));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string host) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}") });
}
