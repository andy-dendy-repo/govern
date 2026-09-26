using System.Net;
using System.Net.Http.Json;
using Govern.Application.Users.Contracts;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Users;

public class GetCurrentUserEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_ValidAccessToken_ReturnsCallersAccount()
    {
        var (email, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.GetMeAsync(Client, tokens.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = (await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>(Ct))!;
        me.Email.ShouldBe(email);
        me.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401ProblemDetails()
    {
        var response = await UsersApi.GetMeAsync(Client, accessToken: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, code: null, Ct);
    }

    [Fact]
    public async Task Get_TamperedToken_Returns401()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var tampered = tokens.AccessToken[..^4] + (tokens.AccessToken.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var response = await UsersApi.GetMeAsync(Client, tampered, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UserDeletedAfterSignIn_Returns404()
    {
        var (email, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            await db.Users.Where(u => u.Email == email).ExecuteDeleteAsync(Ct);
        }

        var response = await UsersApi.GetMeAsync(Client, tokens.AccessToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found", Ct);
    }
}
