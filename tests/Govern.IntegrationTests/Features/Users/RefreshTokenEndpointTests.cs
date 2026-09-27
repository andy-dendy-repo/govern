using System.Net;
using System.Net.Http.Json;
using Govern.Application.Users.Contracts;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Govern.IntegrationTests.Features.Users;

public class RefreshTokenEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_ActiveToken_ReturnsNewPairAndRevokesPresentedToken()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var response = await UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = (await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(Ct))!;
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
        (await UsersApi.GetMeAsync(Client, rotated.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.RefreshTokens.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(Ct);
            stored.Count.ShouldBe(2);
            stored[0].RevokedAt.ShouldNotBeNull();
            stored[0].ReplacedByTokenId.ShouldBe(stored[1].Id);
            stored[1].RevokedAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Post_ReusedRotatedToken_Returns401AndRevokesAllActiveTokens()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var rotated = (await (await UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct)).Content.ReadFromJsonAsync<RefreshTokenResponse>(Ct))!;

        var reuse = await UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct);

        await reuse.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
        var afterTheft = await UsersApi.RefreshAsync(Client, rotated.RefreshToken, Ct);
        await afterTheft.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
    }

    [Fact]
    public async Task Post_SameTokenConcurrently_OnlyOneRotationSucceeds()
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(Client, Ct);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => UsersApi.RefreshAsync(Client, tokens.RefreshToken, Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ExpiredToken_Returns401()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(client, Ct);

        time.Advance(TimeSpan.FromDays(15));
        var response = await UsersApi.RefreshAsync(client, tokens.RefreshToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
    }

    [Fact]
    public async Task Post_UnknownToken_Returns401()
    {
        var response = await UsersApi.RefreshAsync(Client, "not-a-real-refresh-token", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
    }

    [Fact]
    public async Task Post_EmptyToken_Returns400()
    {
        var response = await UsersApi.RefreshAsync(Client, "", Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "RefreshToken");
    }

    [Fact]
    public async Task Post_SignedOutToken_Returns401WithoutRevokingOtherSessions()
    {
        var (email, signedOut) = await UsersApi.CreateSignedInUserAsync(Client, Ct);
        var otherDevice = await UsersApi.SignInTokensAsync(Client, email, Ct);
        (await UsersApi.SignOutAsync(Client, signedOut.AccessToken, signedOut.RefreshToken, Ct)).EnsureSuccessStatusCode();

        var response = await UsersApi.RefreshAsync(Client, signedOut.RefreshToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
        (await UsersApi.RefreshAsync(Client, otherDevice.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_UserHasExpiredTokens_DeletesThemAndKeepsRotatedToken()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var (email, _) = await UsersApi.CreateSignedInUserAsync(client, Ct);
        time.Advance(TimeSpan.FromDays(10));
        var current = await UsersApi.SignInTokensAsync(client, email, Ct);
        time.Advance(TimeSpan.FromDays(5));

        var response = await UsersApi.RefreshAsync(client, current.RefreshToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.RefreshTokens.AsNoTracking().ToListAsync(Ct);
            stored.Count.ShouldBe(2);
            stored.Count(token => token.RevokedAt is not null && token.ReplacedByTokenId is not null).ShouldBe(1);
        }
    }
}
