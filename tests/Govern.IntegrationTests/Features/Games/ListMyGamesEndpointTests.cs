using System.Net;
using System.Net.Http.Json;
using Govern.Application.Games.Contracts;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.Features.Games;

public class ListMyGamesEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Get_ReturnsOnlyCallersGamesMostRecentlyChangedFirst()
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

    private async Task<ListMyGamesResponse> ListAsync(GamesApi.Player caller, string query)
    {
        var response = await GamesApi.ListAsync(Client, caller.AccessToken, query, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListMyGamesResponse>(GamesApi.Json, Ct))!;
    }
}
