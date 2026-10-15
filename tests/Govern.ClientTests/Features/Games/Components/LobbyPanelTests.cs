using System.Net;
using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Games.Components;

public class LobbyPanelTests : ClientTestContext
{
    public LobbyPanelTests() => SignIn();

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Render_Creator_StartNeedsTwoPlayers(int players, bool disabled)
    {
        var cut = RenderPanel(TestGames.CreatorLobby(players));

        cut.Find("button.start").HasAttribute("disabled").ShouldBe(disabled);
        cut.FindAll("button.cancel").Count.ShouldBe(1);
        cut.FindAll("button.leave").ShouldBeEmpty();
    }

    [Fact]
    public void Render_Joiner_OffersOnlyLeave()
    {
        var cut = RenderPanel(TestGames.JoinerLobby());

        cut.FindAll("button.leave").Count.ShouldBe(1);
        cut.FindAll("button.start, button.cancel").ShouldBeEmpty();
    }

    [Fact]
    public void Start_Clicked_ReportsTheStartedGame()
    {
        Api.On(HttpMethod.Post, $"/api/games/{TestGames.Id}/start", () => FakeApiHandler.Json(TestGames.Started()));
        GetGameResponse? started = null;
        var cut = RenderPanel(TestGames.CreatorLobby(players: 2), onStarted: game => started = game);

        cut.Find("button.start").Click();

        cut.WaitForAssertion(() => started.ShouldNotBeNull().Phase.ShouldBe(GamePhase.InProgress));
    }

    [Fact]
    public void Start_Refused_ShowsTheError()
    {
        Api.On(HttpMethod.Post, $"/api/games/{TestGames.Id}/start",
            () => FakeApiHandler.Problem(HttpStatusCode.Conflict, "game.not_enough_players", "The game needs two players to start."));
        var cut = RenderPanel(TestGames.CreatorLobby(players: 2));

        cut.Find("button.start").Click();

        cut.WaitForAssertion(() => cut.Find(".api-error").TextContent.ShouldContain("needs two players"));
    }

    [Fact]
    public void Cancel_Clicked_AsksBeforeSending()
    {
        var cut = RenderPanel(TestGames.CreatorLobby());

        cut.Find("button.cancel").Click();

        cut.Find(".confirm-text").TextContent.ShouldContain("Cancel this game");
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_Confirmed_DeletesTheGameAndReportsClosed()
    {
        Api.On(HttpMethod.Delete, TestGames.GamePath(), () => FakeApiHandler.Empty(HttpStatusCode.NoContent));
        var closed = false;
        var cut = RenderPanel(TestGames.CreatorLobby(), onClosed: () => closed = true);

        cut.Find("button.cancel").Click();
        cut.Find("button.confirm").Click();

        cut.WaitForAssertion(() => closed.ShouldBeTrue());
        Api.Requests.Single().Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public void Leave_Dismissed_SendsNothing()
    {
        var cut = RenderPanel(TestGames.JoinerLobby());

        cut.Find("button.leave").Click();
        cut.Find("button.dismiss").Click();

        cut.FindAll("button.leave").Count.ShouldBe(1);
        Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Leave_Confirmed_PostsLeave()
    {
        Api.On(HttpMethod.Post, $"/api/games/{TestGames.Id}/leave", () => FakeApiHandler.Empty(HttpStatusCode.NoContent));
        var closed = false;
        var cut = RenderPanel(TestGames.JoinerLobby(), onClosed: () => closed = true);

        cut.Find("button.leave").Click();
        cut.Find("button.confirm").Click();

        cut.WaitForAssertion(() => closed.ShouldBeTrue());
    }

    private IRenderedComponent<LobbyPanel> RenderPanel(GetGameResponse game, Action<GetGameResponse>? onStarted = null, Action? onClosed = null) =>
        Render<LobbyPanel>(parameters => parameters
            .Add(static panel => panel.Game, game)
            .Add(static panel => panel.Started, onStarted ?? (static _ => { }))
            .Add(static panel => panel.Closed, onClosed ?? (static () => { })));

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Render_CreatorOfALobbyForThree_StartNeedsEverySeatTaken(int players, bool disabled)
    {
        var cut = RenderPanel(TestGames.CreatorLobby(players) with { Seats = 3 });

        cut.Find("button.start").HasAttribute("disabled").ShouldBe(disabled);
    }
}
