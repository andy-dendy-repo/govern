using System.Net;
using Govern.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;

namespace Govern.IntegrationTests.HealthChecks;

public class HealthEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task GetLive_ReturnsHealthyAnonymously()
    {
        var response = await Client.GetAsync("/health/live", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task GetReady_DatabaseReachable_ReturnsHealthyAnonymously()
    {
        var response = await Client.GetAsync("/health/ready", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task GetOldHealthRoute_IsNoLongerAnonymous()
    {
        var response = await Client.GetAsync("/health", Ct);

        // No endpoint matches, so the fallback policy (authenticated users only) answers before routing can 404.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReady_OverRateLimit_Returns429()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:HealthPermitLimit", "2"));
        using var client = factory.CreateClient();

        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var response = await client.GetAsync("/health/ready", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GetLive_IsNotRateLimited()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:HealthPermitLimit", "1"));
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync("/health/live", Ct)));

        responses.ShouldAllBe(static r => r.StatusCode == HttpStatusCode.OK);
    }
}
