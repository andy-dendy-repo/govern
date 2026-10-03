using System.Net;
using System.Text.Json.Nodes;
using Govern.Client.Features.Games.Clients;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Serialization;
using Govern.Core.Units;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Clients;

public class GamesClientTests : ClientTestContext
{
    private GamesClient Games => Services.GetRequiredService<GamesClient>();

    public GamesClientTests() => SignIn();

    [Fact]
    public async Task CreateAsync_Created_ReturnsTheGameAndCode()
    {
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Json(new { id = TestGames.Id, inviteCode = TestGames.InviteCode }, HttpStatusCode.Created));

        var result = await Games.CreateAsync(Ct);

        result.Value.ShouldBe(new CreateGameResponse(TestGames.Id, TestGames.InviteCode));
        Api.Requests.Single().Authorization.ShouldStartWith("Bearer ");
    }

    [Fact]
    public async Task JoinAsync_Joined_SendsTheCodeInTheBodyOnly()
    {
        Api.On(HttpMethod.Post, "/api/games/join", () => FakeApiHandler.Json(new { gameId = TestGames.Id }));

        var result = await Games.JoinAsync(TestGames.InviteCode, Ct);

        result.Value.ShouldBe(new JoinGameResponse(TestGames.Id));
        var request = Api.Requests.Single();
        request.Path.ShouldBe("/api/games/join");
        request.Body.ShouldBe($$"""{"inviteCode":"{{TestGames.InviteCode}}"}""");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "game.invite_not_found", "No game uses this invite code.")]
    [InlineData(HttpStatusCode.Conflict, "game.full", "The game is full.")]
    [InlineData(HttpStatusCode.Conflict, "game.too_many_open_lobbies", "You already have 3 open lobbies.")]
    [InlineData(HttpStatusCode.Conflict, "game.daily_quota_reached", "You have created or joined 30 games in the last 24 hours.")]
    public async Task JoinAsync_Problem_ReturnsTheCodeAndTitle(HttpStatusCode status, string code, string title)
    {
        Api.On(HttpMethod.Post, "/api/games/join", () => FakeApiHandler.Problem(status, code, title));

        var result = await Games.JoinAsync(TestGames.InviteCode, Ct);

        result.Error.ShouldNotBeNull();
        result.Error.Status.ShouldBe((int)status);
        result.Error.Code.ShouldBe(code);
        result.Error.Message.ShouldBe(title);
    }

    [Fact]
    public async Task LeaveAsync_NoContent_Succeeds()
    {
        Api.On(HttpMethod.Post, $"/api/games/{TestGames.Id}/leave", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));

        (await Games.LeaveAsync(TestGames.Id, Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelAsync_NoContent_SendsDelete()
    {
        Api.On(HttpMethod.Delete, TestGames.GamePath(), () => FakeApiHandler.Empty(HttpStatusCode.NoContent));

        (await Games.CancelAsync(TestGames.Id, Ct)).IsSuccess.ShouldBeTrue();
        Api.Requests.Single().Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public async Task StartAsync_Started_ReturnsTheGame()
    {
        Api.On(HttpMethod.Post, $"/api/games/{TestGames.Id}/start", () => FakeApiHandler.Json(TestGames.Started()));

        var result = await Games.StartAsync(TestGames.Id, Ct);

        result.Value.ShouldNotBeNull().Phase.ShouldBe(GamePhase.InProgress);
    }

    [Fact]
    public async Task GetAsync_Found_ReadsTheGameAndTheViewInTheGameFormat()
    {
        var view = TestViews.Create([TestViews.Settler(1, TestViews.You, TestViews.At(2, 2))], [TestViews.City(8, TestViews.Opponent, TestViews.At(5, 4))]);
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{TestGames.Id}}","phase":"inProgress","isCreator":false,"inviteCode":null,"players":[{"playerId":"{{Guid.Empty}}","seat":1,"isYou":true,"isCreator":false}],"view":{{GameSerializer.Serialize(view)}},"createdAt":"2026-09-28T11:00:00+00:00"}""",
                System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await Games.GetAsync(TestGames.Id, Ct);

        var game = result.Value.ShouldNotBeNull();
        game.Phase.ShouldBe(GamePhase.InProgress);
        game.Players.Single().IsYou.ShouldBeTrue();
        GameSerializer.Serialize(game.View.ShouldNotBeNull()).ShouldBe(GameSerializer.Serialize(view));
    }

    [Fact]
    public async Task SendCommandAsync_Move_PostsTheVersionAndTheCommandInTheGameFormat()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));

        var result = await Games.SendCommandAsync(TestGames.Id, 4242, new MoveUnitCommand(new UnitId(1), new HexCoord(4, 1)), Ct);

        GameSerializer.Serialize(result.Value.ShouldNotBeNull().View).ShouldBe(GameSerializer.Serialize(TestViews.Start()));
        var request = Api.Requests.Single();
        request.Authorization.ShouldStartWith("Bearer ");
        JsonNode.DeepEquals(
                JsonNode.Parse(request.Body!),
                JsonNode.Parse("""{"expectedVersion":4242,"command":{"type":"move_unit","unitId":{"value":1},"target":{"q":4,"r":1}}}"""))
            .ShouldBeTrue(request.Body);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task SendCommandAsync_AnyCommand_TheGameFormatReadsItBack(GameCommand command)
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));

        await Games.SendCommandAsync(TestGames.Id, 1, command, Ct);

        var body = JsonNode.Parse(Api.Requests.Single().Body!)!;
        GameSerializer.Deserialize<GameCommand>(body["command"]!.ToJsonString()).ShouldBe(command);
    }

    public static TheoryData<GameCommand> Commands => new()
    {
        new MoveUnitCommand(new UnitId(1), new HexCoord(4, 1)),
        new AttackCommand(new UnitId(2), new HexCoord(-1, 3)),
        new FoundCityCommand(new UnitId(1), "Ambermoor"),
        new SetProductionCommand(new CityId(5), TestViews.SoldierType),
        new EndTurnCommand(),
        new ResignCommand(),
    };

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "game.stale_version", "The game changed since you last saw it.")]
    [InlineData(HttpStatusCode.Conflict, "move.unreachable", "The unit cannot reach that tile this turn.")]
    [InlineData(HttpStatusCode.Conflict, "game.not_your_turn", "It is not your turn.")]
    [InlineData(HttpStatusCode.NotFound, "game.not_found", "The game was not found.")]
    public async Task SendCommandAsync_Problem_ReturnsTheCodeAndTitle(HttpStatusCode status, string code, string title)
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Problem(status, code, title));

        var result = await Games.SendCommandAsync(TestGames.Id, 1, new EndTurnCommand(), Ct);

        result.Error.ShouldNotBeNull().Code.ShouldBe(code);
        result.Error.Message.ShouldBe(title);
    }

    [Fact]
    public async Task SendCommandAsync_RateLimited_ReturnsTheGenericMessage()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        var result = await Games.SendCommandAsync(TestGames.Id, 1, new EndTurnCommand(), Ct);

        result.Error.ShouldNotBeNull().Status.ShouldBe(429);
    }

    [Fact]
    public async Task SendCommandAsync_NetworkFailure_ReturnsTheNetworkError()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => throw new HttpRequestException("offline"));

        var result = await Games.SendCommandAsync(TestGames.Id, 1, new EndTurnCommand(), Ct);

        result.Error.ShouldNotBeNull().IsNetwork.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsTheError()
    {
        Api.On(HttpMethod.Get, TestGames.GamePath(), () => FakeApiHandler.Problem(HttpStatusCode.NotFound, "game.not_found", "The game was not found."));

        var result = await Games.GetAsync(TestGames.Id, Ct);

        result.Error.ShouldNotBeNull().Code.ShouldBe("game.not_found");
    }

    [Fact]
    public async Task ListAsync_Page_SendsPageAndSize()
    {
        var item = new MyGameItem(TestGames.Id, GamePhase.Lobby, 1, true, false, TestGames.CreatedAt);
        Api.On(HttpMethod.Get, "/api/games?page=2&pageSize=20", () => FakeApiHandler.Json(new ListMyGamesResponse([item], 21)));

        var result = await Games.ListAsync(2, 20, Ct);

        result.Value.ShouldNotBeNull().Total.ShouldBe(21);
        result.Value.Items.ShouldBe([item]);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task ListAsync_OutOfRange_Throws(int page, int pageSize)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Games.ListAsync(page, pageSize, Ct));
    }

    [Fact]
    public async Task CreateAsync_RateLimitedWithoutBody_ReturnsTheGenericMessage()
    {
        Api.On(HttpMethod.Post, "/api/games", () => FakeApiHandler.Empty(HttpStatusCode.TooManyRequests));

        var result = await Games.CreateAsync(Ct);

        result.Error.ShouldNotBeNull().Status.ShouldBe(429);
        result.Error.Message.ShouldContain("Too many requests");
    }

    [Fact]
    public async Task CreateAsync_FailsAfterTheCallerCancelled_Throws()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Api.On(HttpMethod.Post, "/api/games", () =>
        {
            cancellation.Cancel();
            throw new HttpRequestException("aborted");
        });

        await Should.ThrowAsync<Exception>(() => Games.CreateAsync(cancellation.Token));
    }

    [Fact]
    public async Task CreateAsync_NetworkFailure_ReturnsTheNetworkError()
    {
        Api.On(HttpMethod.Post, "/api/games", () => throw new HttpRequestException("offline"));

        var result = await Games.CreateAsync(Ct);

        result.Error.ShouldNotBeNull().IsNetwork.ShouldBeTrue();
    }
}
