using Govern.Api.Extensions;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.ScaleOut;

public class GameChangeBackplaneTests(GovernApiFixture fixture, RedisFixture redis) : ScaleOutTestBase(fixture, redis)
{
    [Fact]
    public async Task ChangeOnInstanceA_IsPushedToStreamsOnBothInstances()
    {
        var a = StartInstance();
        var b = StartInstance();
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(a, Ct);
        await using var creatorOnA = await GameEventsReader.OpenAsync(a, creator.AccessToken, game.Id, Ct);
        await using var joinerOnB = await GameEventsReader.OpenAsync(b, joiner.AccessToken, game.Id, Ct);
        await creatorOnA.NextGameAsync();
        (await joinerOnB.NextGameAsync()).Phase.ShouldBe(GamePhase.Lobby);

        (await GamesApi.StartAsync(a, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();

        (await creatorOnA.NextGameAsync()).Phase.ShouldBe(GamePhase.InProgress);
        (await joinerOnB.NextGameAsync()).Phase.ShouldBe(GamePhase.InProgress);
    }

    [Fact]
    public async Task EndTurnOnInstanceB_ReachesTheOpponentOnInstanceA()
    {
        var a = StartInstance();
        var b = StartInstance();
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(a, Ct);
        await using var joinerOnA = await GameEventsReader.OpenAsync(a, joiner.AccessToken, game.Id, Ct);
        var before = await joinerOnA.NextGameAsync();

        await SendAsync(b, creator, game.Id, new EndTurnCommand());

        var after = await joinerOnA.NextGameAsync();
        after.View.ShouldNotBeNull().CurrentPlayer.ShouldBe(new PlayerId(before.Players.Single(p => p.IsYou).PlayerId));
    }

    [Fact]
    public async Task ChangeMissedDuringADisconnect_IsPushedAfterTheReconnect()
    {
        // Instance A runs without Redis, so its change never reaches the channel: exactly what a message lost while B was
        // disconnected looks like to B. Only B's resync after its subscription reconnects can deliver it.
        var a = StartInstance(redisConnectionString: null);
        var b = StartInstance();
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(a, Ct);
        await using var joinerOnB = await GameEventsReader.OpenAsync(b, joiner.AccessToken, game.Id, Ct);
        var before = await joinerOnB.NextGameAsync();
        await SendAsync(a, creator, game.Id, new EndTurnCommand());

        await Redis.KillSubscriptionConnectionsAsync();

        var after = await joinerOnB.NextGameAsync();
        after.View.ShouldNotBeNull().CurrentPlayer.ShouldBe(new PlayerId(before.Players.Single(p => p.IsYou).PlayerId));
    }

    [Fact]
    public async Task ReconnectWithoutAChange_PushesNothing()
    {
        var a = StartInstance();
        var b = StartInstance(new Dictionary<string, string> { ["GameStream:HeartbeatInterval"] = "00:00:00.200" });
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(a, Ct);
        await using var joinerOnB = await GameEventsReader.OpenAsync(b, joiner.AccessToken, game.Id, Ct);
        var before = await joinerOnB.NextGameAsync();

        await Redis.KillSubscriptionConnectionsAsync();

        // The resync re-reads the game, but the joiner's view didn't change, so only heartbeats follow.
        for (var i = 0; i < 5; i++)
        {
            (await joinerOnB.NextEventAsync()).EventType.ShouldBe(AsyncEnumerableExtensions.HeartbeatEventType);
        }

        await SendAsync(a, creator, game.Id, new EndTurnCommand());
        (await joinerOnB.NextGameAsync()).View.ShouldNotBeNull().CurrentPlayer
            .ShouldBe(new PlayerId(before.Players.Single(p => p.IsYou).PlayerId));
    }

    private static async Task SendAsync(HttpClient client, GamesApi.Player player, Guid gameId, GameCommand command)
    {
        var version = (await GamesApi.GetGameAsync(client, player, gameId, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(client, player.AccessToken, gameId, version, command, Ct)).EnsureSuccessStatusCode();
    }
}
