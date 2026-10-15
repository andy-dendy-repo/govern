using System.Net;
using System.Net.Http.Json;
using Govern.Application.Games.Contracts;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Games;

public class ListMyGamesEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_ReturnsOnlyCallersGamesMostRecentActivityFirst()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        var joinedLater = await GamesApi.CreateGameAsync(Client, me, Ct);
        var mine = await GamesApi.CreateGameAsync(Client, me, Ct);
        var theirs = await GamesApi.CreateGameAsync(Client, other, Ct);
        await GamesApi.CreateGameAsync(Client, other, Ct);
        (await GamesApi.JoinAsync(Client, me.AccessToken, theirs.InviteCode, Ct)).EnsureSuccessStatusCode();
        (await GamesApi.JoinAsync(Client, other.AccessToken, joinedLater.InviteCode, Ct)).EnsureSuccessStatusCode();

        var body = await ListAsync(me, string.Empty);

        body.Total.ShouldBe(3);
        body.Items.Select(i => i.Id).ShouldBe([joinedLater.Id, theirs.Id, mine.Id]);
        body.Items.Select(i => (i.PlayerCount, i.IsCreator, i.Phase))
            .ShouldBe([(2, true, GamePhase.Lobby), (2, false, GamePhase.Lobby), (1, true, GamePhase.Lobby)]);
    }

    [Fact]
    public async Task Get_StartedGame_IsListedAsInProgress()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.ListAsync(Client, creator.AccessToken, string.Empty, Ct);

        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var item = document.RootElement.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("id").GetGuid().ShouldBe(game.Id);
        item.GetProperty("phase").GetString().ShouldBe("inProgress");
        item.GetProperty("playerCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Get_Paged_ReturnsRequestedSliceAndTotal()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Insert(0, (await GamesApi.CreateGameAsync(Client, me, Ct)).Id);
        }

        var first = await ListAsync(me, "?page=1&pageSize=2");
        var second = await ListAsync(me, "?page=2&pageSize=2");

        first.Items.Select(i => i.Id).ShouldBe(ids[..2]);
        second.Items.Select(i => i.Id).ShouldBe(ids[2..]);
        second.Total.ShouldBe(3);
    }

    [Fact]
    public async Task Get_NoGames_ReturnsEmptyPage()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);

        var body = await ListAsync(me, string.Empty);

        body.Items.ShouldBeEmpty();
        body.Total.ShouldBe(0);
    }

    [Theory]
    [InlineData("?pageSize=0", "PageSize")]
    [InlineData("?pageSize=51", "PageSize")]
    [InlineData("?page=0", "Page")]
    [InlineData("?page=10001", "Page")]
    public async Task Get_OutOfRangePaging_Returns400(string query, string field)
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.ListAsync(Client, me.AccessToken, query, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, field);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var response = await GamesApi.ListAsync(Client, accessToken: null, string.Empty, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_RunningGame_FlagsYourTurnForThePlayerToMoveOnly()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        (await ListAsync(creator, "")).Items.ShouldHaveSingleItem().IsYourTurn.ShouldBeTrue();
        (await ListAsync(joiner, "")).Items.ShouldHaveSingleItem().IsYourTurn.ShouldBeFalse();

        var version = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new EndTurnCommand(), Ct)).EnsureSuccessStatusCode();

        (await ListAsync(creator, "")).Items.ShouldHaveSingleItem().IsYourTurn.ShouldBeFalse();
        (await ListAsync(joiner, "")).Items.ShouldHaveSingleItem().IsYourTurn.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_FinishedGame_IsNobodysTurn()
    {
        var (game, creator, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var version = (await GamesApi.GetGameAsync(Client, creator, game.Id, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, creator.AccessToken, game.Id, version, new ResignCommand(), Ct)).EnsureSuccessStatusCode();

        foreach (var player in new[] { creator, joiner })
        {
            var item = (await ListAsync(player, "")).Items.ShouldHaveSingleItem();
            item.Phase.ShouldBe(GamePhase.Finished);
            item.IsYourTurn.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Get_MoveLeavesActivityAndOrderAloneButEndTurnMovesTheGameToTheTop()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var older = await StartGameWithNewOpponentAsync(me);
        var newer = await StartGameWithNewOpponentAsync(me);
        var before = await ListAsync(me, "");

        await SendAsync(me, older, await AnyMoveAsync(older, me));
        var afterMove = await ListAsync(me, "");
        await SendAsync(me, older, new EndTurnCommand());
        var afterEndTurn = await ListAsync(me, "");

        before.Items.Select(i => i.Id).ShouldBe([newer, older]);
        afterMove.Items.ShouldBe(before.Items);
        afterEndTurn.Items.Select(i => i.Id).ShouldBe([older, newer]);
        afterEndTurn.Items[0].ActivityAt.ShouldBeGreaterThan(before.Items[1].ActivityAt);
    }

    private async Task<Guid> StartGameWithNewOpponentAsync(GamesApi.Player creator)
    {
        var opponent = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);
        (await GamesApi.JoinAsync(Client, opponent.AccessToken, game.InviteCode, Ct)).EnsureSuccessStatusCode();
        (await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        return game.Id;
    }

    private async Task<GameCommand> AnyMoveAsync(Guid gameId, GamesApi.Player mover)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var game = await db.Games.AsNoTracking().Include(g => g.Players).SingleAsync(g => g.Id == gameId, Ct);
            var seat = game.FindPlayer(mover.UserId)!.PlayerId;
            return game.State!.Units
                .Where(unit => unit.Owner == seat)
                .SelectMany(unit => GameEngine.ReachableTiles(TestRulesets.Default, game.State, seat, unit.Id).Select(target => new MoveUnitCommand(unit.Id, target)))
                .First();
        }
    }

    private async Task SendAsync(GamesApi.Player player, Guid gameId, GameCommand command)
    {
        var version = (await GamesApi.GetGameAsync(Client, player, gameId, Ct)).View!.Version;
        (await GamesApi.SendCommandAsync(Client, player.AccessToken, gameId, version, command, Ct)).EnsureSuccessStatusCode();
    }

    private async Task<ListMyGamesResponse> ListAsync(GamesApi.Player caller, string query)
    {
        var response = await GamesApi.ListAsync(Client, caller.AccessToken, query, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListMyGamesResponse>(GamesApi.Json, Ct))!;
    }

    [Fact]
    public async Task Get_ResignedFromAGameThatGoesOn_IsListedAsOutForThatPlayerOnly()
    {
        var (game, players) = await GamesApi.CreateLobbyForAsync(Client, seats: 3, joiners: 2, Ct);
        (await GamesApi.StartAsync(Client, players[0].AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();
        var version = (await GamesApi.GetGameAsync(Client, players[1], game.Id, Ct)).View!.Version;

        (await GamesApi.SendCommandAsync(Client, players[1].AccessToken, game.Id, version, new ResignCommand(), Ct)).EnsureSuccessStatusCode();

        var resigned = (await ListAsync(players[1], "")).Items.ShouldHaveSingleItem();
        resigned.Phase.ShouldBe(GamePhase.InProgress);
        resigned.IsOut.ShouldBeTrue();
        resigned.Seats.ShouldBe(3);
        var creator = (await ListAsync(players[0], "")).Items.ShouldHaveSingleItem();
        creator.IsOut.ShouldBeFalse();
        creator.IsYourTurn.ShouldBeTrue();
    }
}
