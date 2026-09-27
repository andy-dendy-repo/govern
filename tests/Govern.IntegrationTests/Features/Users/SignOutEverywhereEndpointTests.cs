using System.Net;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Users;

public class SignOutEverywhereEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_Authenticated_Returns204AndNoSessionRefreshes()
    {
        var (email, first) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var second = await UsersApi.SignInTokensAsync(Client, email, Ct);

        var response = await UsersApi.SignOutEverywhereAsync(Client, first.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UsersApi.RefreshAsync(Client, first.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await UsersApi.RefreshAsync(Client, second.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Authenticated_DoesNotAffectOtherUsers()
    {
        var (_, caller) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var (_, other) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        (await UsersApi.SignOutEverywhereAsync(Client, caller.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await UsersApi.RefreshAsync(Client, other.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ConcurrentWithRefresh_LeavesNoActiveToken()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var responses = await Task.WhenAll(
            UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct),
            UsersApi.SignOutEverywhereAsync(Client, tokens.AccessToken, Ct));

        responses[1].StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            (await db.RefreshTokens.AsNoTracking().CountAsync(token => token.RevokedAt == null, Ct)).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Post_WithoutAccessToken_Returns401()
    {
        var response = await UsersApi.SignOutEverywhereAsync(Client, accessToken: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
