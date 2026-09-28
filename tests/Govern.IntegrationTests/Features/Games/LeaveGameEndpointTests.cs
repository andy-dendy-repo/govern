using System.Net;
using Govern.Application.Games.Models;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Games;

public class LeaveGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_Joiner_Returns204AndFreesTheSeat()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await GamesApi.GetAsync(Client, joiner.AccessToken, game.Id, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
        var seen = await GamesApi.GetGameAsync(Client, creator, game.Id, Ct);
        seen.Players.ShouldHaveSingleItem().IsYou.ShouldBeTrue();
    }

    [Fact]
    public async Task Post_ThenJoinAgain_TakesSeatOne()
    {
        var (game, _, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        (await GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seen = await GamesApi.GetGameAsync(Client, joiner, game.Id, Ct);
        seen.Players.Single(player => player.IsYou).Seat.ShouldBe(1);
    }

    [Fact]
    public async Task Post_UserAtUnfinishedCap_CanJoinAnotherGameAfterLeaving()
    {
        var host = await GamesApi.NewPlayerAsync(Client, Ct);
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, host.UserId, me.UserId, GameErrors.MaxUnfinishedGamesPerUser - 1, Ct);
        var joined = await GamesApi.CreateGameAsync(Client, await GamesApi.NewPlayerAsync(Client, Ct), Ct);
        (await GamesApi.JoinAsync(Client, me.AccessToken, joined.InviteCode, Ct)).EnsureSuccessStatusCode();
        var wanted = await GamesApi.CreateGameAsync(Client, await GamesApi.NewPlayerAsync(Client, Ct), Ct);
        await (await GamesApi.JoinAsync(Client, me.AccessToken, wanted.InviteCode, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_unfinished_games", Ct);

        (await GamesApi.LeaveAsync(Client, me.AccessToken, joined.Id, Ct)).EnsureSuccessStatusCode();

        (await GamesApi.JoinAsync(Client, me.AccessToken, wanted.InviteCode, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Creator_Returns409CreatorCannotLeave()
    {
        var (game, creator, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, creator.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.creator_cannot_leave", Ct);
        (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).Players.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Post_StartedGame_Returns409AlreadyStarted()
    {
        var (game, _, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
    }

    [Fact]
    public async Task Post_LeaveWinsARaceWithStart_StartGetsNotEnoughPlayers()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var (leave, start) = await GameRowLock.RaceInOrderAsync(
            Fixture.ConnectionString,
            game.Id,
            () => GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct),
            () => GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct),
            Ct);

        leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await start.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.not_enough_players", Ct);
    }

    [Fact]
    public async Task Post_StartWinsARaceWithLeave_LeaveGetsAlreadyStarted()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var (start, leave) = await GameRowLock.RaceInOrderAsync(
            Fixture.ConnectionString,
            game.Id,
            () => GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct),
            () => GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct),
            Ct);

        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        await leave.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
    }

    [Fact]
    public async Task Post_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, outsider.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_UnknownGame_Returns404()
    {
        var player = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, player.AccessToken, Guid.CreateVersion7(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_EmptyGameId_Returns400()
    {
        var player = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, player.AccessToken, Guid.Empty, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.LeaveAsync(Client, accessToken: null, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
