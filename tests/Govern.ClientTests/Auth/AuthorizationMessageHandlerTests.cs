using System.Net;
using System.Net.Http.Json;
using Govern.Client.Auth;
using Govern.Client.Features.Users.Clients;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Auth;

public class AuthorizationMessageHandlerTests : ClientTestContext
{
    private const string MePath = "/api/users/me";
    private const string RefreshPath = "/api/users/refresh";

    // The typed client's pipeline: AuthorizationMessageHandler in front of the fake Api.
    private HttpClient Http => Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(UsersClient));

    [Fact]
    public async Task Send_SignedIn_AddsTheBearerToken()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.OK));

        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Api.Requests.ShouldHaveSingleItem().Authorization.ShouldBe($"Bearer {tokens.AccessToken}");
    }

    [Fact]
    public async Task Send_SendAsIsRequestRejected_SendsNoTokenAndDoesNotRefresh()
    {
        SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/users/sign-in");
        request.Options.Set(AuthorizationMessageHandler.SendAsIs, true);

        using var response = await Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Api.Requests.ShouldHaveSingleItem().Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task Send_Unauthorized_RefreshesOnceAndRetriesWithTheNewTokenAndBody()
    {
        var old = SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, "/api/games", request => Task.FromResult(FakeApiHandler.Empty(
            request.Headers.Authorization?.Parameter == fresh.AccessToken ? HttpStatusCode.Created : HttpStatusCode.Unauthorized)));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(fresh));

        using var response = await Http.PostAsJsonAsync("api/games", new { name = "x" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attempts = Api.RequestsTo("/api/games");
        attempts.Select(static attempt => attempt.Authorization).ShouldBe([$"Bearer {old.AccessToken}", $"Bearer {fresh.AccessToken}"]);
        attempts.ShouldAllBe(static attempt => attempt.Body == "{\"name\":\"x\"}");
    }

    [Fact]
    public async Task Send_UnauthorizedAgainAfterRefresh_ReturnsTheSecond401WithoutAnotherRetry()
    {
        SignIn();
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(TestTokens.Pair("player@example.com", Time.GetUtcNow())));

        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Api.RequestsTo(MePath).Count.ShouldBe(2);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Send_ConcurrentUnauthorizedRequests_ShareOneRefresh()
    {
        SignIn();
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Get, MePath, request => Task.FromResult(FakeApiHandler.Empty(
            request.Headers.Authorization?.Parameter == fresh.AccessToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(fresh));

        var responses = await Task.WhenAll(Http.GetAsync("api/users/me", Ct), Http.GetAsync("api/users/me", Ct));

        responses.ShouldAllBe(static response => response.StatusCode == HttpStatusCode.OK);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Send_RefreshRejected_Returns401AndSignsOut()
    {
        SignIn();
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task Send_RefreshUnavailable_Returns503AndKeepsTheSession()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        StoredTokens().ShouldBe(tokens);
    }

    [Fact]
    public async Task Send_RenewalDue_RefreshesBeforeSending()
    {
        SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Json(fresh));
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.OK));

        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Api.Requests.Select(static request => request.Path).ShouldBe([RefreshPath, MePath]);
        Api.RequestsTo(MePath).Single().Authorization.ShouldBe($"Bearer {fresh.AccessToken}");
    }

    [Fact]
    public async Task Send_OtherOrigin_SendsNoTokenAndDoesNotRefresh()
    {
        SignIn();
        Api.On(HttpMethod.Get, "/leak", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        using var response = await Http.GetAsync("https://evil.example/leak", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Api.Requests.ShouldHaveSingleItem().Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task Send_RefreshUnavailableTwice_CallsTheRefreshEndpointOnceWithinTheBackoff()
    {
        SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        Api.On(HttpMethod.Post, RefreshPath, () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));
        Api.On(HttpMethod.Get, MePath, () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        using var first = await Http.GetAsync("api/users/me", Ct);
        using var second = await Http.GetAsync("api/users/me", Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        second.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        Api.RequestsTo(RefreshPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Send_SignedOut_Returns401WithoutCallingTheApi()
    {
        using var response = await Http.GetAsync("api/users/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Api.Requests.ShouldBeEmpty();
    }
}
