using System.Net;
using Govern.Client.Features.Users.Clients;
using Govern.Client.Features.Users.Models;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Users.Clients;

public class UsersClientTests : ClientTestContext
{
    private UsersClient Users => Services.GetRequiredService<UsersClient>();

    [Fact]
    public async Task SignUpAsync_Created_ReturnsTheUserIdWithoutSigningIn()
    {
        var userId = Guid.CreateVersion7();
        Api.On(HttpMethod.Post, "/api/users/sign-up", () => FakeApiHandler.Json(new { userId }, HttpStatusCode.Created));

        var result = await Users.SignUpAsync(new SignUpRequest("new@example.com", "password1"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
        Api.Requests.ShouldHaveSingleItem().Body.ShouldBe("""{"email":"new@example.com","password":"password1"}""");
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignUpAsync_EmailTaken_ReturnsTheCodeAndTitle()
    {
        Api.On(HttpMethod.Post, "/api/users/sign-up",
            () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "user.email_taken", "An account with this email already exists."));

        var result = await Users.SignUpAsync(new SignUpRequest("taken@example.com", "password1"), Ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("user.email_taken");
        result.Error.Message.ShouldBe("An account with this email already exists.");
    }

    [Fact]
    public async Task SignInAsync_ValidCredentials_StoresTheSession()
    {
        var pair = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, "/api/users/sign-in", () => FakeApiHandler.Json(pair));

        var result = await Users.SignInAsync(new SignInRequest { Email = "player@example.com", Password = "password1" }, Ct);

        result.IsSuccess.ShouldBeTrue();
        var stored = StoredTokens().ShouldNotBeNull();
        stored.AccessToken.ShouldBe(pair.AccessToken);
        stored.RefreshToken.ShouldBe(pair.RefreshToken);
        Api.Requests.ShouldHaveSingleItem().Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SignInAsync_InvalidCredentials_ReturnsTheErrorWithoutRefreshing()
    {
        SignIn("someone-else@example.com");
        Api.On(HttpMethod.Post, "/api/users/sign-in",
            () => FakeApiHandler.Problem(HttpStatusCode.Unauthorized, "user.invalid_credentials", "Invalid email or password."));

        var result = await Users.SignInAsync(new SignInRequest { Email = "player@example.com", Password = "wrong" }, Ct);

        result.Error.ShouldNotBeNull().Code.ShouldBe("user.invalid_credentials");
        Api.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetCurrentUserAsync_SignedIn_ReturnsTheUser()
    {
        SignIn();
        var createdAt = Time.GetUtcNow().AddDays(-3);
        Api.On(HttpMethod.Get, "/api/users/me", () => FakeApiHandler.Json(new { id = Guid.CreateVersion7(), email = "player@example.com", createdAt }));

        var result = await Users.GetCurrentUserAsync(Ct);

        result.Value.ShouldNotBeNull().Email.ShouldBe("player@example.com");
        result.Value.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public async Task GetCurrentUserAsync_NetworkError_ReturnsTheNetworkError()
    {
        SignIn();
        Api.On(HttpMethod.Get, "/api/users/me", _ => throw new HttpRequestException("offline"));

        var result = await Users.GetCurrentUserAsync(Ct);

        result.Error.ShouldNotBeNull().IsNetwork.ShouldBeTrue();
    }

    [Fact]
    public async Task SignOutAsync_SignedIn_RevokesThisSessionAndClearsIt()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));

        var result = await Users.SignOutAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        Api.Requests.ShouldHaveSingleItem().Body.ShouldBe($"{{\"refreshToken\":\"{tokens.RefreshToken}\"}}");
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignOutAsync_RenewalDue_RefreshesFirstThenSendsTheRotatedTokens()
    {
        SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        var fresh = TestTokens.Pair("player@example.com", Time.GetUtcNow());
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Json(fresh));
        Api.On(HttpMethod.Post, "/api/users/sign-out", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));

        var result = await Users.SignOutAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        Api.Requests.Select(static request => request.Path).ShouldBe(["/api/users/refresh", "/api/users/sign-out"]);
        var signOut = Api.RequestsTo("/api/users/sign-out").Single();
        signOut.Body.ShouldBe($"{{\"refreshToken\":\"{fresh.RefreshToken}\"}}");
        signOut.Authorization.ShouldBe($"Bearer {fresh.AccessToken}");
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignOutAsync_RefreshRejected_ClearsTheSessionWithoutSendingTheSignOut()
    {
        SignIn();
        Time.Advance(TimeSpan.FromMinutes(14));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        var result = await Users.SignOutAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        Api.RequestsTo("/api/users/sign-out").ShouldBeEmpty();
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignOutAsync_ApiFails_StillClearsTheSession()
    {
        SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out", () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));

        var result = await Users.SignOutAsync(Ct);

        result.IsSuccess.ShouldBeFalse();
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignOutEverywhereAsync_ApiFails_KeepsTheSessionSoTheUserCanRetry()
    {
        var tokens = SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out-everywhere", () => FakeApiHandler.Empty(HttpStatusCode.InternalServerError));

        var result = await Users.SignOutEverywhereAsync(Ct);

        result.Error.ShouldNotBeNull().Status.ShouldBe(500);
        StoredTokens().ShouldBe(tokens);
    }

    [Fact]
    public async Task SignOutEverywhereAsync_SessionNoLongerAccepted_ClearsTheSession()
    {
        SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out-everywhere", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));
        Api.On(HttpMethod.Post, "/api/users/refresh", () => FakeApiHandler.Empty(HttpStatusCode.Unauthorized));

        var result = await Users.SignOutEverywhereAsync(Ct);

        result.Error.ShouldNotBeNull().Status.ShouldBe(401);
        StoredTokens().ShouldBeNull();
    }

    [Fact]
    public async Task SignOutEverywhereAsync_NoContent_SucceedsAndClearsTheSession()
    {
        SignIn();
        Api.On(HttpMethod.Post, "/api/users/sign-out-everywhere", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));

        var result = await Users.SignOutEverywhereAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        StoredTokens().ShouldBeNull();
    }
}
