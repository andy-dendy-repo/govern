using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Security;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace Govern.IntegrationTests.OpenApi;

public class SwaggerUiTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task GetSwaggerUi_Development_ReturnsThePageAnonymously()
    {
        var response = await Client.GetAsync("/swagger/index.html", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Govern API");
    }

    [Fact]
    public async Task GetSwaggerUiConfig_Development_PointsAtTheOpenApiDocument()
    {
        var response = await Client.GetAsync("/swagger/index.js", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("/openapi/v1.json");
    }

    [Fact]
    public async Task GetOpenApi_Development_DeclaresTheBearerScheme()
    {
        using var document = await ReadDocumentAsync();

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        scheme.GetProperty("type").GetString().ShouldBe("http");
        scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
    }

    [Fact]
    public async Task GetOpenApi_Development_RequiresBearerExactlyOnNonAnonymousOperations()
    {
        using var document = await ReadDocumentAsync();

        // Unresolved security references are dropped silently when serialised, so check every operation of the real document.
        var mismatches = (
            from path in document.RootElement.GetProperty("paths").EnumerateObject()
            from operation in path.Value.EnumerateObject()
            let anonymous = EndpointAuthorizationTests.IntentionallyAnonymous.Contains(path.Name)
            where anonymous == RequiresBearer(operation.Value)
            select $"{operation.Name.ToUpperInvariant()} {path.Name}").ToList();

        mismatches.ShouldBeEmpty("Operations without AllowAnonymous must require Bearer, and anonymous ones must not");
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Get_OutsideDevelopment_Returns404ForSignedInUser(string path)
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder
            .UseEnvironment("Production")
            .UseSetting("HostFiltering:AllowedHosts:0", "localhost"));
        using var client = factory.CreateClient();
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(client, Ct);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(request, Ct);

        // A signed-in user passes the fallback policy, so only a path nothing serves answers 404.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<JsonDocument> ReadDocumentAsync()
    {
        var response = await Client.GetAsync("/openapi/v1.json", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static bool RequiresBearer(JsonElement operation) =>
        operation.TryGetProperty("security", out var security)
        && security.EnumerateArray().Any(static requirement => requirement.TryGetProperty("Bearer", out _));
}
