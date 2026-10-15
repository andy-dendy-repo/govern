using System.Net;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.ScaleOut;

public class SharedRateLimitTests(GovernApiFixture fixture, RedisFixture redis) : ScaleOutTestBase(fixture, redis)
{
    [Fact]
    public async Task FixedWindow_RequestsOnBothInstancesShareOneLimit()
    {
        var settings = new Dictionary<string, string> { ["RateLimiting:HealthPermitLimit"] = "3" };
        var a = StartInstance(settings);
        var b = StartInstance(settings);

        var statuses = new List<HttpStatusCode>();
        foreach (var client in new[] { a, b, a, b })
        {
            using var response = await client.GetAsync("/health/ready", Ct);
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task OpenStreams_AreCappedAcrossInstancesAndFreedWhenOneCloses()
    {
        var settings = new Dictionary<string, string> { ["RateLimiting:GameStreamsConcurrentLimit"] = "2" };
        var a = StartInstance(settings);
        var b = StartInstance(settings);
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(a, Ct);
        var first = await GameEventsReader.OpenAsync(a, creator.AccessToken, game.Id, Ct);
        await using var second = await GameEventsReader.OpenAsync(a, creator.AccessToken, game.Id, Ct);

        using (var rejected = await GameEventsReader.RequestAsync(b, creator.AccessToken, game.Id, Ct))
        {
            rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        await first.DisposeAsync();

        await using var third = await OpenWhenFreedAsync(b, creator.AccessToken, game.Id);
    }

    // The server releases a stream's permit when it notices the client went away, which takes a moment.
    private static async Task<GameEventsReader> OpenWhenFreedAsync(HttpClient client, string accessToken, Guid gameId)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        var response = await GameEventsReader.RequestAsync(client, accessToken, gameId, Ct);
        while (response.StatusCode == HttpStatusCode.TooManyRequests && DateTimeOffset.UtcNow < deadline)
        {
            response.Dispose();
            await Task.Delay(100, Ct);
            response = await GameEventsReader.RequestAsync(client, accessToken, gameId, Ct);
        }

        return await GameEventsReader.ReadAsync(response, Ct);
    }
}
