using System.Net;
using System.Net.Http.Json;
using Govern.Application.Games.Contracts;
using Govern.Application.Games.Models;
using Govern.Core.Actions;
using Govern.Core.State;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Games;

public class CreateGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_SignedIn_Returns201AndPersistsLobbyWithCreatorAtSeatZero()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateAsync(Client, creator.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<CreateGameResponse>(GamesApi.Json, Ct))!;
        response.Headers.Location?.ToString().ShouldBe($"/api/games/{body.Id}");
        body.InviteCode.Length.ShouldBe(22);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var game = await db.Games.AsNoTracking().Include(g => g.Players).SingleAsync(g => g.Id == body.Id, Ct);
            game.Phase.ShouldBe(GamePhase.Lobby);
            game.CreatorId.ShouldBe(creator.UserId);
            game.InviteCode.Value.ShouldBe(body.InviteCode);
            game.State.ShouldBeNull();
            var seat = game.Players.ShouldHaveSingleItem();
            seat.UserId.ShouldBe(creator.UserId);
            seat.Seat.ShouldBe(0);
        }
    }

    [Fact]
    public async Task Post_WithoutBody_ChoosesARandomPoolMap()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var body = await GamesApi.CreateGameAsync(Client, creator, Ct);

        (await StoredMapAsync(body.Id)).ShouldBe("random");
        (await GamesApi.GetGameAsync(Client, creator, body.Id, Ct)).Map.ShouldBe("random");
    }

    [Theory]
    [InlineData("basin")]
    [InlineData("generated")]
    [InlineData("random")]
    [InlineData(null)]
    public async Task Post_WithAMapChoice_Returns201AndStoresIt(string? map)
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithMapAsync(Client, creator.AccessToken, map, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<CreateGameResponse>(GamesApi.Json, Ct))!;
        (await StoredMapAsync(body.Id)).ShouldBe(map ?? "random");
    }

    [Theory]
    [InlineData("atlantis")]
    [InlineData("")]
    [InlineData("BASIN")]
    public async Task Post_WithAMapNotInThePool_Returns400(string map)
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithMapAsync(Client, creator.AccessToken, map, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Map");
    }

    [Fact]
    public async Task Post_WithAMapWithoutToken_Returns401()
    {
        var response = await GamesApi.CreateWithMapAsync(Client, accessToken: null, "basin", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<string> StoredMapAsync(Guid gameId)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            return (await db.Games.AsNoTracking().SingleAsync(g => g.Id == gameId, Ct)).Map.Value;
        }
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var response = await GamesApi.CreateAsync(Client, accessToken: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AtOpenLobbyLimit_Returns409()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        for (var i = 0; i < GameErrors.MaxOpenLobbiesPerUser; i++)
        {
            await GamesApi.CreateGameAsync(Client, creator, Ct);
        }

        var response = await GamesApi.CreateAsync(Client, creator.AccessToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_open_lobbies", Ct);
    }

    [Fact]
    public async Task Post_AfterStartingALobbyAtTheLimit_Returns201()
    {
        var (_, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        for (var i = 0; i < GameErrors.MaxOpenLobbiesPerUser - 1; i++)
        {
            await GamesApi.CreateGameAsync(Client, creator, Ct);
        }

        var response = await GamesApi.CreateAsync(Client, creator.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AtUnfinishedGameLimit_Returns409()
    {
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        var caller = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, other.UserId, caller.UserId, GameErrors.MaxUnfinishedGamesPerUser, Ct);

        var response = await GamesApi.CreateAsync(Client, caller.AccessToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_unfinished_games", Ct);
    }

    [Fact]
    public async Task Post_AtUnfinishedGameLimitAfterAGameFinished_Returns201()
    {
        var (game, caller, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, other.UserId, caller.UserId, GameErrors.MaxUnfinishedGamesPerUser - 1, Ct);
        await (await GamesApi.CreateAsync(Client, caller.AccessToken, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_unfinished_games", Ct);
        var version = (await GamesApi.GetGameAsync(Client, caller, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, caller.AccessToken, game.Id, version, new ResignCommand(), Ct)).EnsureSuccessStatusCode();

        var response = await GamesApi.CreateAsync(Client, caller.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_OverGamesRateLimit_Returns429PerUserAcrossCreateJoinAndStart()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(static builder => builder.UseSetting("RateLimiting:GamesPermitLimit", "2"));
        using var client = factory.CreateClient();
        var creator = await GamesApi.NewPlayerAsync(client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(client, Ct);
        var host = await GamesApi.NewPlayerAsync(client, Ct);
        var game = await GamesApi.CreateGameAsync(client, creator, Ct);
        (await GamesApi.JoinAsync(client, joiner.AccessToken, game.InviteCode, Ct)).EnsureSuccessStatusCode();
        (await GamesApi.CreateAsync(client, joiner.AccessToken, Ct)).EnsureSuccessStatusCode();
        (await GamesApi.StartAsync(client, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        var hostLobby = await GamesApi.CreateGameAsync(client, host, Ct);

        var joinLimited = await GamesApi.JoinAsync(client, joiner.AccessToken, hostLobby.InviteCode, Ct);
        var createLimited = await GamesApi.CreateAsync(client, creator.AccessToken, Ct);
        var otherUser = await GamesApi.CreateAsync(client, host.AccessToken, Ct);

        joinLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        createLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        otherUser.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_QuotaOfSeatsInTheLastDayReached_Returns429DailyQuotaReached()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var opponent = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedFinishedGamesAsync(
            Fixture, opponent.UserId, me.UserId, GameErrors.MaxGamesJoinedPerWindow - 1, DateTimeOffset.UtcNow.AddHours(-1), Ct);
        (await GamesApi.CreateAsync(Client, me.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await GamesApi.CreateAsync(Client, me.AccessToken, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "game.daily_quota_reached", Ct);
    }

    [Fact]
    public async Task Post_SeatsOlderThanTheQuotaWindow_DoNotCount()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var opponent = await GamesApi.NewPlayerAsync(Client, Ct);
        var outsideWindow = DateTimeOffset.UtcNow - GameErrors.QuotaWindow - TimeSpan.FromMinutes(5);
        await GamesApi.SeedFinishedGamesAsync(Fixture, opponent.UserId, me.UserId, GameErrors.MaxGamesJoinedPerWindow, outsideWindow, Ct);

        var response = await GamesApi.CreateAsync(Client, me.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Post_WithSeats_Returns201AndStoresThem(int seats)
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithSeatsAsync(Client, creator.AccessToken, "random", seats, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<CreateGameResponse>(GamesApi.Json, Ct))!;
        (await GamesApi.GetGameAsync(Client, creator, body.Id, Ct)).Seats.ShouldBe(seats);
    }

    [Fact]
    public async Task Post_WithoutSeats_IsForTwoPlayers()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var body = await GamesApi.CreateGameAsync(Client, creator, Ct);

        (await GamesApi.GetGameAsync(Client, creator, body.Id, Ct)).Seats.ShouldBe(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Post_WithSeatsOutsideTheRulesetsRange_Returns400(int seats)
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithSeatsAsync(Client, creator.AccessToken, "random", seats, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Seats");
    }

    [Fact]
    public async Task Post_WithSimultaneousTurns_Returns201AndStoresTheTurnMode()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithTurnModeAsync(Client, creator.AccessToken, "simultaneous", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<CreateGameResponse>(GamesApi.Json, Ct))!;
        (await GamesApi.GetGameAsync(Client, creator, body.Id, Ct)).TurnMode.ShouldBe(TurnMode.Simultaneous);
    }

    [Fact]
    public async Task Post_WithoutATurnMode_IsSequential()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var body = await GamesApi.CreateGameAsync(Client, creator, Ct);

        (await GamesApi.GetGameAsync(Client, creator, body.Id, Ct)).TurnMode.ShouldBe(TurnMode.Sequential);
    }

    [Fact]
    public async Task Post_WithAnUnknownTurnMode_Returns400()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithTurnModeAsync(Client, creator.AccessToken, "dynamic", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithAPoolMapMadeForAnotherNumberOfPlayers_Returns400()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CreateWithSeatsAsync(Client, creator.AccessToken, "basin", 3, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Map");
    }

    [Fact]
    public async Task Post_WithSeatsWithoutToken_Returns401()
    {
        var response = await GamesApi.CreateWithSeatsAsync(Client, accessToken: null, "random", 3, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AtUnfinishedGameLimitAfterResigningFromAGameThatGoesOn_Returns201()
    {
        var (game, players) = await GamesApi.CreateLobbyForAsync(Client, seats: 3, joiners: 2, Ct);
        (await GamesApi.StartAsync(Client, players[0].AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        var caller = players[2];
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, other.UserId, caller.UserId, GameErrors.MaxUnfinishedGamesPerUser - 1, Ct);
        await (await GamesApi.CreateAsync(Client, caller.AccessToken, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_unfinished_games", Ct);
        var version = (await GamesApi.GetGameAsync(Client, caller, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, caller.AccessToken, game.Id, version, new ResignCommand(), Ct)).EnsureSuccessStatusCode();

        var response = await GamesApi.CreateAsync(Client, caller.AccessToken, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await GamesApi.GetGameAsync(Client, players[0], game.Id, Ct)).Phase.ShouldBe(GamePhase.InProgress);
    }
}
