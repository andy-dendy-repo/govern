using System.Diagnostics;
using Govern.Core.Actions;
using Govern.Domain.Entities;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Govern.IntegrationTests.Background;

public class ExpiredLobbyCleanupServiceTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly TimeSpan SweepTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Sweep_DeletesOnlyLobbiesWithoutActivityForTheLobbyLifetime()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("LobbyCleanup:Interval", "00:01:00")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var creator = await GamesApi.NewPlayerAsync(client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(client, Ct);
        var longAgo = time.GetUtcNow() - Game.LobbyLifetime - TimeSpan.FromHours(1);
        var stale = Game.Create(creator.UserId, longAgo);
        var joinedRecently = Game.Create(creator.UserId, longAgo);
        joinedRecently.Join(joiner.UserId, time.GetUtcNow().AddDays(-1));
        var startedLongAgo = Game.Create(creator.UserId, longAgo);
        startedLongAgo.Join(joiner.UserId, longAgo);
        startedLongAgo.Start(creator.UserId, longAgo);
        await SeedAsync(stale, joinedRecently, startedLongAgo);
        await using var stream = await GameEventsReader.OpenAsync(client, creator.AccessToken, stale.Id, Ct);
        await stream.NextGameAsync();

        time.Advance(TimeSpan.FromMinutes(1));

        await WaitUntilDeletedAsync(stale.Id);
        (await GamesApi.ExistsAsync(Fixture, joinedRecently.Id, Ct)).ShouldBeTrue();
        (await GamesApi.ExistsAsync(Fixture, startedLongAgo.Id, Ct)).ShouldBeTrue();
        await stream.ShouldEndAsync();
    }

    [Fact]
    public async Task Sweep_FinishedGamesAreKept()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("LobbyCleanup:Interval", "00:01:00")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var creator = await GamesApi.NewPlayerAsync(client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(client, Ct);
        var longAgo = time.GetUtcNow() - Game.LobbyLifetime - TimeSpan.FromHours(1);
        var finished = Game.Create(creator.UserId, longAgo);
        finished.Join(joiner.UserId, longAgo);
        finished.Start(creator.UserId, longAgo);
        finished.Execute(joiner.UserId, new ResignCommand(), longAgo);
        var stale = Game.Create(creator.UserId, longAgo);
        await SeedAsync(finished, stale);

        time.Advance(TimeSpan.FromMinutes(1));

        await WaitUntilDeletedAsync(stale.Id);
        (await GamesApi.ExistsAsync(Fixture, finished.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Sweep_MoreStaleLobbiesThanTheBatchSize_DeletesThemAll()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("LobbyCleanup:Interval", "00:01:00")
            .UseSetting("LobbyCleanup:BatchSize", "1")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var creator = await GamesApi.NewPlayerAsync(client, Ct);
        var longAgo = time.GetUtcNow() - Game.LobbyLifetime - TimeSpan.FromHours(1);
        var stale = Enumerable.Range(0, 3).Select(_ => Game.Create(creator.UserId, longAgo)).ToArray();
        await SeedAsync(stale);

        time.Advance(TimeSpan.FromMinutes(1));

        foreach (var game in stale)
        {
            await WaitUntilDeletedAsync(game.Id);
        }
    }

    private async Task SeedAsync(params Game[] games)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            db.Games.AddRange(games);
            await db.SaveChangesAsync(Ct);
        }
    }

    // The sweep runs on the background service's own thread once the fake clock passes the interval.
    private async Task WaitUntilDeletedAsync(Guid gameId)
    {
        var clock = Stopwatch.StartNew();
        while (await GamesApi.ExistsAsync(Fixture, gameId, Ct))
        {
            clock.Elapsed.ShouldBeLessThan(SweepTimeout, "the expired lobby was not deleted");
            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }
}
