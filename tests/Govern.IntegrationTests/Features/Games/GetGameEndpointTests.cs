using System.Net;
using System.Text.Json;
using Govern.Core.Engine;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Games;

public class GetGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_LobbyAsJoiner_ReturnsSeatsWithoutInviteCodeOrView()
    {
        var (game, _, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.GetAsync(Client, joiner.AccessToken, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await GamesApi.ReadGameAsync(response, Ct);
        body.Id.ShouldBe(game.Id);
        body.Phase.ShouldBe(GamePhase.Lobby);
        body.IsCreator.ShouldBeFalse();
        body.InviteCode.ShouldBeNull();
        body.View.ShouldBeNull();
        body.Players.Select(p => (p.Seat, p.IsYou, p.IsCreator)).ShouldBe([(0, false, true), (1, true, false)]);
    }

    [Fact]
    public async Task Get_LobbyAsCreator_ReturnsInviteCode()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        var body = await GamesApi.ReadGameAsync(await GamesApi.GetAsync(Client, creator.AccessToken, game.Id, Ct), Ct);

        body.IsCreator.ShouldBeTrue();
        body.InviteCode.ShouldBe(game.InviteCode);
        body.Players.ShouldHaveSingleItem().IsYou.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_Started_ReturnsEachPlayerTheirOwnFogOfWarView()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var mine = await GamesApi.ReadGameAsync(await GamesApi.GetAsync(Client, creator.AccessToken, game.Id, Ct), Ct);
        var theirs = await GamesApi.ReadGameAsync(await GamesApi.GetAsync(Client, joiner.AccessToken, game.Id, Ct), Ct);

        mine.Phase.ShouldBe(GamePhase.InProgress);
        mine.InviteCode.ShouldBeNull();
        mine.IsCreator.ShouldBeTrue();
        var myView = mine.View.ShouldNotBeNull();
        var theirView = theirs.View.ShouldNotBeNull();
        myView.Viewer.ShouldBe(new PlayerId(mine.Players.Single(p => p.IsYou).PlayerId));
        theirView.Viewer.ShouldBe(new PlayerId(theirs.Players.Single(p => p.IsYou).PlayerId));
        myView.Viewer.ShouldNotBe(theirView.Viewer);

        // The spawns are far apart, so neither player sees the other's units at the start.
        myView.Units.ShouldAllBe(unit => unit.Owner == myView.Viewer);
        theirView.Units.ShouldAllBe(unit => unit.Owner == theirView.Viewer);
        myView.Units.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Get_Started_ViewJsonEqualsTheGameSerializerFormat()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.GetAsync(Client, creator.AccessToken, game.Id, Ct);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        document.RootElement.GetProperty("phase").GetString().ShouldBe("inProgress");

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.Games.AsNoTracking().Include(g => g.Players).SingleAsync(g => g.Id == game.Id, Ct);
            var expected = GameEngine.ViewFor(TestRulesets.Default, stored.State!, stored.FindPlayer(creator.UserId)!.PlayerId);
            using var expectedJson = JsonDocument.Parse(GameSerializer.Serialize(expected));

            JsonElement.DeepEquals(document.RootElement.GetProperty("view"), expectedJson.RootElement).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Get_Started_ResponseContainsNoUserIds()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        foreach (var caller in new[] { creator, joiner })
        {
            var json = await (await GamesApi.GetAsync(Client, caller.AccessToken, game.Id, Ct)).Content.ReadAsStringAsync(Ct);

            json.ShouldNotContain(creator.UserId.ToString(), Case.Insensitive);
            json.ShouldNotContain(joiner.UserId.ToString(), Case.Insensitive);
        }
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.GetAsync(Client, accessToken: null, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.GetAsync(Client, outsider.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Get_EmptyGameId_Returns400()
    {
        var caller = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.GetAsync(Client, caller.AccessToken, Guid.Empty, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }
}
