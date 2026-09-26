using System.Net;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.IntegrationTests.Security;

public class EndpointAuthorizationTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly string[] IntentionallyAnonymous =
    [
        "/api/users/sign-up",
        "/api/users/sign-in",
        "/api/users/refresh",
        "/health",
        "/openapi/{documentName}.json",
    ];

    [Fact]
    public void Endpoints_AllDeclareAccessExplicitly()
    {
        var undeclared = RouteEndpoints()
            .Where(static e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(static e => e.RoutePattern.RawText)
            .ToList();

        undeclared.ShouldBeEmpty("Every endpoint must call RequireAuthorization(...) or AllowAnonymous() (see security.md)");
    }

    [Fact]
    public void Endpoints_OnlyIntendedOnesAreAnonymous()
    {
        var anonymous = RouteEndpoints()
            .Where(static e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(static e => e.RoutePattern.RawText)
            .ToList();

        anonymous.ShouldBe(IntentionallyAnonymous, ignoreOrder: true);
    }

    [Fact]
    public async Task Post_SignInOverRateLimit_Returns429()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthPermitLimit", "2"));
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await UsersApi.SignInAsync(client, UsersApi.NewEmail(), UsersApi.Password, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var limited = await UsersApi.SignInAsync(client, UsersApi.NewEmail(), UsersApi.Password, Ct);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private List<RouteEndpoint> RouteEndpoints() =>
        Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
}
