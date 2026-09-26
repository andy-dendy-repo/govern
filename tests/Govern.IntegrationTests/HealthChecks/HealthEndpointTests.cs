using System.Net;
using Govern.IntegrationTests.Fixtures;

namespace Govern.IntegrationTests.HealthChecks;

public class HealthEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_DatabaseReachable_ReturnsHealthyAnonymously()
    {
        var response = await Client.GetAsync("/health", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }
}
