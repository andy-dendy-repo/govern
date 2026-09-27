using System.Net;
using System.Net.Http.Json;
using Govern.Application.Games.Contracts;
using Govern.Application.Games.Models;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
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
}
