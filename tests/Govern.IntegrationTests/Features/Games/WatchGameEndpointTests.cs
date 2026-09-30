using System.Net;
using Govern.Api.Extensions;
using Govern.Application.Games.Contracts;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Govern.IntegrationTests.Features.Games;

public class WatchGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_Lobby_SendsTheSnapshotThenTheJoinAndTheStart()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);
        await using var stream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);

        var lobby = await stream.NextGameAsync();
        (await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct)).EnsureSuccessStatusCode();
        var joined = await stream.NextGameAsync();
        (await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        var started = await stream.NextGameAsync();

        lobby.Id.ShouldBe(game.Id);
        lobby.Phase.ShouldBe(GamePhase.Lobby);
        lobby.Players.ShouldHaveSingleItem().IsYou.ShouldBeTrue();
        lobby.InviteCode.ShouldBe(game.InviteCode);
        joined.Players.Count.ShouldBe(2);
        joined.View.ShouldBeNull();
        started.Phase.ShouldBe(GamePhase.InProgress);
        started.InviteCode.ShouldBeNull();
        started.View.ShouldNotBeNull().Viewer.ShouldBe(new PlayerId(started.Players.Single(p => p.IsYou).PlayerId));
    }

    [Fact]
    public async Task Get_JoinerLeaves_CreatorGetsTheOneSeatLobbyAndTheLeaversStreamEnds()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        await using var creatorStream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);
        await using var joinerStream = await GameEventsReader.OpenAsync(Client, joiner.AccessToken, game.Id, Ct);
        await creatorStream.NextGameAsync();
        await joinerStream.NextGameAsync();

        (await GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();

        (await creatorStream.NextGameAsync()).Players.ShouldHaveSingleItem().IsYou.ShouldBeTrue();
        await joinerStream.ShouldEndAsync();
    }

    [Fact]
    public async Task Get_CreatorCancels_BothStreamsEnd()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        await using var creatorStream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);
        await using var joinerStream = await GameEventsReader.OpenAsync(Client, joiner.AccessToken, game.Id, Ct);
        await creatorStream.NextGameAsync();
        await joinerStream.NextGameAsync();

        (await GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();

        await creatorStream.ShouldEndAsync();
        await joinerStream.ShouldEndAsync();
    }

    [Fact]
    public async Task Get_EndTurn_PushesTheNewTurnToBothPlayersWithTheirOwnViews()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        await using var creatorStream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);
        await using var joinerStream = await GameEventsReader.OpenAsync(Client, joiner.AccessToken, game.Id, Ct);
        var creatorBefore = await creatorStream.NextGameAsync();
        await joinerStream.NextGameAsync();

        await SendAsync(Client, creator, game.Id, new EndTurnCommand());
        var creatorAfter = await creatorStream.NextGameAsync();
        var joinerAfter = await joinerStream.NextGameAsync();

        var joinerSeat = new PlayerId(joinerAfter.Players.Single(p => p.IsYou).PlayerId);
        creatorAfter.View.ShouldNotBeNull().CurrentPlayer.ShouldBe(joinerSeat);
        creatorAfter.View.Version.ShouldNotBe(creatorBefore.View!.Version);
        joinerAfter.View.ShouldNotBeNull().CurrentPlayer.ShouldBe(joinerSeat);
        joinerAfter.View.Viewer.ShouldBe(joinerSeat);
        joinerAfter.View.Version.ShouldBe((await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct)).View!.Version);
    }

    [Fact]
    public async Task Get_MoveHiddenFromTheOpponent_IsPushedToTheMoverOnly()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("GameStream:HeartbeatInterval", "00:00:00.200"));
        using var client = factory.CreateClient();
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(client, Ct);
        await using var creatorStream = await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct);
        await using var joinerStream = await GameEventsReader.OpenAsync(client, joiner.AccessToken, game.Id, Ct);
        var start = await creatorStream.NextGameAsync();
        await joinerStream.NextGameAsync();
        var move = await HiddenMoveAsync(game.Id, start);

        await SendAsync(client, creator, game.Id, move);
        var moved = await creatorStream.NextGameAsync();

        moved.View.ShouldNotBeNull().Units.Single(u => u.Id == move.UnitId).Position.ShouldBe(move.Target);
        for (var i = 0; i < 3; i++)
        {
            (await joinerStream.NextEventAsync()).EventType.ShouldBe(AsyncEnumerableExtensions.HeartbeatEventType);
        }

        // The decisive check: had the hidden move been pushed, that snapshot would be read here first, with the creator to move.
        await SendAsync(client, creator, game.Id, new EndTurnCommand());
        (await joinerStream.NextGameAsync()).View!.CurrentPlayer.ShouldBe(new PlayerId(start.Players.Single(p => !p.IsYou).PlayerId));
    }

    [Fact]
    public async Task Get_Resign_SendsTheFinishedSnapshotAndEndsTheStream()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        await using var stream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);
        await stream.NextGameAsync();

        await SendAsync(Client, joiner, game.Id, new ResignCommand());
        var finished = await stream.NextGameAsync();

        finished.Phase.ShouldBe(GamePhase.Finished);
        finished.View.ShouldNotBeNull().Winner.ShouldBe(finished.View.Viewer);
        await stream.ShouldEndAsync();
    }

    [Fact]
    public async Task Get_FinishedGame_SendsTheSnapshotAndEnds()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        await SendAsync(Client, joiner, game.Id, new ResignCommand());

        await using var stream = await GameEventsReader.OpenAsync(Client, creator.AccessToken, game.Id, Ct);

        (await stream.NextGameAsync()).Phase.ShouldBe(GamePhase.Finished);
        await stream.ShouldEndAsync();
    }

    [Fact]
    public async Task Get_AccessTokenExpires_EndsTheStream()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(client, Ct);
        await using var stream = await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct);
        (await stream.NextGameAsync()).Id.ShouldBe(game.Id);

        time.Advance(TimeSpan.FromMinutes(16));

        await stream.ShouldEndAsync();
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        using var response = await GameEventsReader.RequestAsync(Client, null, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        using var response = await GameEventsReader.RequestAsync(Client, outsider.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Get_UnknownGame_Returns404()
    {
        var caller = await GamesApi.NewPlayerAsync(Client, Ct);

        using var response = await GameEventsReader.RequestAsync(Client, caller.AccessToken, Guid.CreateVersion7(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Get_EmptyId_Returns400()
    {
        var caller = await GamesApi.NewPlayerAsync(Client, Ct);

        using var response = await GameEventsReader.RequestAsync(Client, caller.AccessToken, Guid.Empty, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }

    [Fact]
    public async Task Get_OverConcurrentLimit_Returns429UntilAStreamCloses()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:GameStreamsConcurrentLimit", "1"));
        using var client = factory.CreateClient();
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(client, Ct);
        var first = await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct);
        await first.NextGameAsync();

        using var limited = await GameEventsReader.RequestAsync(client, creator.AccessToken, game.Id, Ct);
        await using var otherUser = await GameEventsReader.OpenAsync(client, joiner.AccessToken, game.Id, Ct);
        await first.DisposeAsync();

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        using var reopened = await RetryUntilOkAsync(() => GameEventsReader.RequestAsync(client, creator.AccessToken, game.Id, Ct));
        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_StreamsHeldPastTheWindow_KeepTheConcurrentLimit()
    {
        // The rate limiter drops partitions it reports idle (checked on its own real-time timer, after about 10 s), so this
        // waits in real time: with a refilled 1 s window, the open stream must still keep the partition and its limit alive.
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder
            .UseSetting("RateLimiting:GameStreamsConcurrentLimit", "1")
            .UseSetting("RateLimiting:GameStreamsWindow", "00:00:01"));
        using var client = factory.CreateClient();
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(client, Ct);
        await using var open = await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct);
        await open.NextGameAsync();

        await Task.Delay(TimeSpan.FromSeconds(13), Ct);
        using var limited = await GameEventsReader.RequestAsync(client, creator.AccessToken, game.Id, Ct);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Get_OverOpeningWindow_Returns429()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:GameStreamsPermitLimit", "2"));
        using var client = factory.CreateClient();
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(client, Ct);
        await using (await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct))
        await using (await GameEventsReader.OpenAsync(client, creator.AccessToken, game.Id, Ct))
        {
        }

        using var limited = await GameEventsReader.RequestAsync(client, creator.AccessToken, game.Id, Ct);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // A move of one of the caller's units that leaves the opponent's view (and so its version) unchanged.
    private async Task<MoveUnitCommand> HiddenMoveAsync(Guid gameId, GetGameResponse caller)
    {
        var state = await StoredStateAsync(gameId);
        var mover = new PlayerId(caller.Players.Single(p => p.IsYou).PlayerId);
        var opponent = new PlayerId(caller.Players.Single(p => !p.IsYou).PlayerId);
        var opponentVersion = GameEngine.ViewFor(TestRulesets.Default, state, opponent).Version;

        return state.Units
            .Where(unit => unit.Owner == mover)
            .SelectMany(unit => GameEngine.ReachableTiles(TestRulesets.Default, state, mover, unit.Id).Select(target => new MoveUnitCommand(unit.Id, target)))
            .FirstOrDefault(move => GameEngine.ViewFor(TestRulesets.Default, GameEngine.Execute(TestRulesets.Default, state, mover, move).Update!.State, opponent).Version == opponentVersion)
            .ShouldNotBeNull("the starting position has no move hidden from the opponent");
    }

    // Streams are woken by the Api instance that saved the change, so commands go through the stream's own client.
    private static async Task SendAsync(HttpClient client, GamesApi.Player player, Guid gameId, GameCommand command)
    {
        var version = (await GamesApi.GetGameAsync(client, player, gameId, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(client, player.AccessToken, gameId, version, command, Ct)).EnsureSuccessStatusCode();
    }

    private async Task<GameState> StoredStateAsync(Guid gameId)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            return (await db.Games.AsNoTracking().SingleAsync(g => g.Id == gameId, Ct)).State!;
        }
    }

    // The server releases a closed stream's rate-limit permit shortly after the client goes away.
    private static async Task<HttpResponseMessage> RetryUntilOkAsync(Func<Task<HttpResponseMessage>> send)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var response = await send();
            if (response.StatusCode == HttpStatusCode.OK || timeout.IsCancellationRequested)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }
}
