using System.Net;
using System.Net.Http.Json;
using Govern.Application.Users.Contracts;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Users;

public class SignOutEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_OwnToken_Returns204AndTokenNoLongerRefreshes()
    {
        var (email, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var otherDevice = await UsersApi.SignInTokensAsync(Client, email, Ct);

        var response = await UsersApi.SignOutAsync(Client, tokens.AccessToken, tokens.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
        (await UsersApi.RefreshAsync(Client, otherDevice.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_UnknownToken_Returns204()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.SignOutAsync(Client, tokens.AccessToken, "not-a-real-refresh-token", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AnotherUsersToken_Returns204AndDoesNotRevokeIt()
    {
        var (_, caller) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var (_, victim) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.SignOutAsync(Client, caller.AccessToken, victim.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UsersApi.RefreshAsync(Client, victim.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_EmptyToken_Returns400()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.SignOutAsync(Client, tokens.AccessToken, "", Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "RefreshToken");
    }

    [Fact]
    public async Task Post_WithoutAccessToken_Returns401AndKeepsToken()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.SignOutAsync(Client, accessToken: null, tokens.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_RotatedToken_Returns204AndRevokesAllSessions()
    {
        var (email, stolen) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var otherDevice = await UsersApi.SignInTokensAsync(Client, email, Ct);
        var attacker = (await (await UsersApi.RefreshAsync(Client, stolen.RefreshToken, Ct)).Content.ReadFromJsonAsync<RefreshTokenResponse>(Ct))!;

        var response = await UsersApi.SignOutAsync(Client, stolen.AccessToken, stolen.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UsersApi.RefreshAsync(Client, attacker.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await UsersApi.RefreshAsync(Client, otherDevice.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
