using System.Net;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Govern.IntegrationTests.ScaleOut;

/// <summary>An instance whose Redis is unreachable starts and keeps serving, with per-instance state.</summary>
public class RedisOutageTests(GovernApiFixture fixture, RedisFixture redis) : ScaleOutTestBase(fixture, redis)
{
    // Nothing listens on port 1; a short connect timeout keeps startup quick.
    private const string UnreachableRedis = "127.0.0.1:1,connectTimeout=500";

    [Fact]
    public async Task GetReady_RedisUnreachable_ReportsDegradedButServes()
    {
        var client = StartInstance(redisConnectionString: UnreachableRedis);

        using var response = await client.GetAsync("/health/ready", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Degraded");
    }

    [Fact]
    public async Task GetReady_RedisReachable_ReportsHealthy()
    {
        var client = StartInstance();

        using var response = await client.GetAsync("/health/ready", Ct);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task RedisUnreachable_GamesStreamsAndSignInStillWork()
    {
        var client = StartInstance(redisConnectionString: UnreachableRedis);
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(client, Ct);
        await using var stream = await GameEventsReader.OpenAsync(client, joiner.AccessToken, game.Id, Ct);
        var before = await stream.NextGameAsync();

        var version = (await GamesApi.GetGameAsync(client, creator, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(client, creator.AccessToken, game.Id, version, new EndTurnCommand(), Ct)).EnsureSuccessStatusCode();

        (await stream.NextGameAsync()).View.ShouldNotBeNull().CurrentPlayer
            .ShouldBe(new PlayerId(before.Players.Single(p => p.IsYou).PlayerId));
    }

    [Fact]
    public async Task RedisUnreachable_RateLimitFallbackKeepsItsWindowWhileTheLimiterIsIdle()
    {
        var client = StartInstance(
            new Dictionary<string, string> { ["RateLimiting:HealthPermitLimit"] = "2" }, redisConnectionString: UnreachableRedis);
        for (var i = 0; i < 2; i++)
        {
            using var allowed = await client.GetAsync("/health/ready", Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // ASP.NET Core drops a partition whose limiter reports idle for more than 10 s; that must not reset the window.
        await Task.Delay(TimeSpan.FromSeconds(11), Ct);

        using var response = await client.GetAsync("/health/ready", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void Startup_RedisWithoutTlsOutsideDevelopment_Fails()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("HostFiltering:AllowedHosts:0", "localhost");
            builder.UseSetting("ConnectionStrings:Redis", UnreachableRedis);
        });

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("ssl=true");
    }

    [Fact]
    public async Task RedisUnreachable_SignInThrottlingFallsBackToThisInstance()
    {
        var client = StartInstance(redisConnectionString: UnreachableRedis);
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();
        for (var i = 0; i < 5; i++)
        {
            (await UsersApi.SignInAsync(client, email, "wrong-password", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var response = await UsersApi.SignInAsync(client, email, UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "user.too_many_sign_in_attempts", Ct);
    }
}
