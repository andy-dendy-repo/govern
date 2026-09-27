using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Govern.Application.Users.Contracts;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Govern.IntegrationTests.Features.Users;

public class SignInEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_ValidCredentials_ReturnsTokensAndStoresOnlyRefreshTokenHash()
    {
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();

        var response = await UsersApi.SignInAsync(Client, email, UsersApi.Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<SignInResponse>(Ct))!;
        tokens.AccessTokenExpiresAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        tokens.RefreshTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(13));

        var jwt = new JsonWebToken(tokens.AccessToken);
        jwt.Issuer.ShouldBe("govern-api");
        jwt.GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe(email);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.RefreshTokens.AsNoTracking().SingleAsync(Ct);
            var expectedHash = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(tokens.RefreshToken)));
            stored.TokenHash.ShouldBe(expectedHash);
            stored.RevokedAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Post_WrongPassword_Returns401()
    {
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();

        var response = await UsersApi.SignInAsync(Client, email, "Wrong-password-1", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_credentials", Ct);
    }

    [Fact]
    public async Task Post_UnknownEmail_Returns401WithSameErrorAsWrongPassword()
    {
        var response = await UsersApi.SignInAsync(Client, UsersApi.NewEmail(), UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_credentials", Ct);
    }

    [Fact]
    public async Task Post_EmailInDifferentCaseWithWhitespace_Returns200()
    {
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();

        var response = await UsersApi.SignInAsync(Client, $"  {email.ToUpperInvariant()} ", UsersApi.Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<SignInResponse>(Ct))!;
        new JsonWebToken(tokens.AccessToken).GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe(email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("user\r\n@example.com")]
    [InlineData("user\u200B@example.com")]
    public async Task Post_MalformedEmail_Returns401WithSameErrorAsWrongPassword(string email)
    {
        var response = await UsersApi.SignInAsync(Client, email, UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_credentials", Ct);
    }

    [Fact]
    public async Task Post_MissingFields_Returns400WithFieldErrors()
    {
        var response = await UsersApi.SignInAsync(Client, "", "", Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Email", "Password");
    }

    [Fact]
    public async Task Post_BeyondSessionCap_RevokesLeastRecentlyUsedSessionOnly()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("Jwt:MaxActiveRefreshTokens", "2")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var (email, first) = await UsersApi.CreateSignedInUserAsync(client, Ct);
        time.Advance(TimeSpan.FromMinutes(1));
        var second = await UsersApi.SignInTokensAsync(client, email, Ct);
        time.Advance(TimeSpan.FromMinutes(1));

        var third = await UsersApi.SignInTokensAsync(client, email, Ct);

        // The evicted session is rejected without triggering reuse detection, so the other sessions keep working.
        await (await UsersApi.RefreshAsync(client, first.RefreshToken, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "user.invalid_refresh_token", Ct);
        (await UsersApi.RefreshAsync(client, second.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UsersApi.RefreshAsync(client, third.RefreshToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_UserHasExpiredTokens_DeletesThem()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var (email, _) = await UsersApi.CreateSignedInUserAsync(client, Ct);
        await UsersApi.SignInTokensAsync(client, email, Ct);
        time.Advance(TimeSpan.FromDays(15));

        var current = await UsersApi.SignInTokensAsync(client, email, Ct);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.RefreshTokens.AsNoTracking().SingleAsync(Ct);
            stored.ExpiresAt.ShouldBe(current.RefreshTokenExpiresAt);
        }
    }

    [Fact]
    public async Task Post_AtSessionCapConcurrentlyWithRefreshOfEvictedToken_NeverFails()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:MaxActiveRefreshTokens", "1"));
        using var client = factory.CreateClient();
        var (email, oldest) = await UsersApi.CreateSignedInUserAsync(client, Ct);

        var responses = await Task.WhenAll(
            UsersApi.SignInAsync(client, email, UsersApi.Password, Ct),
            UsersApi.SignInAsync(client, email, UsersApi.Password, Ct),
            UsersApi.RefreshAsync(client, oldest.RefreshToken, Ct));

        responses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.OK);
        responses[2].StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }
}
