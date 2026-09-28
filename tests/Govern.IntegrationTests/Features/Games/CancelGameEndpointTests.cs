using System.Net;
using Govern.Application.Games.Models;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Games;

public class CancelGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Delete_Creator_Returns204AndDeletesTheLobby()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GamesApi.ExistsAsync(Fixture, game.Id, Ct)).ShouldBeFalse();
        foreach (var player in new[] { creator, joiner })
        {
            await (await GamesApi.GetAsync(Client, player.AccessToken, game.Id, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
        }
    }

    [Fact]
    public async Task Delete_ThenJoinWithTheInviteCode_Returns404InviteNotFound()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);
        (await GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.invite_not_found", Ct);
    }

    [Fact]
    public async Task Delete_AtOpenLobbyCap_LetsTheCreatorCreateAgain()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var lobbies = new List<Guid>();
        for (var i = 0; i < GameErrors.MaxOpenLobbiesPerUser; i++)
        {
            lobbies.Add((await GamesApi.CreateGameAsync(Client, creator, Ct)).Id);
        }

        await (await GamesApi.CreateAsync(Client, creator.AccessToken, Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_open_lobbies", Ct);

        (await GamesApi.CancelAsync(Client, creator.AccessToken, lobbies[0], Ct)).EnsureSuccessStatusCode();

        (await GamesApi.CreateAsync(Client, creator.AccessToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_Joiner_Returns403NotCreator()
    {
        var (game, _, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, joiner.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "game.not_creator", Ct);
        (await GamesApi.ExistsAsync(Fixture, game.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_StartedGame_Returns409AlreadyStarted()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
        (await GamesApi.ExistsAsync(Fixture, game.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_ConcurrentlyWithTheJoinerLeaving_ExactlyOneWins()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var responses = await GameRowLock.RaceAsync(Fixture.ConnectionString, game.Id, 2, () => Task.WhenAll(
            GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct),
            GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct)), Ct);

        // Whichever runs second finds the game changed: a cancel after the leave reports a stale view, a leave after the
        // cancel finds no game.
        var (cancel, leave) = (responses[0], responses[1]);
        if (cancel.StatusCode == HttpStatusCode.NoContent)
        {
            await leave.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
        }
        else
        {
            leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            await cancel.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.stale_version", Ct);
        }
    }

    [Fact]
    public async Task Delete_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, outsider.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Delete_UnknownGame_Returns404()
    {
        var player = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, player.AccessToken, Guid.CreateVersion7(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Delete_EmptyGameId_Returns400()
    {
        var player = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, player.AccessToken, Guid.Empty, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.CancelAsync(Client, accessToken: null, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
