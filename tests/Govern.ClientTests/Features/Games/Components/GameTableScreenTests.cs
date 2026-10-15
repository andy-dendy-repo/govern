using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The full-screen game view: the top strip, the selection card, the forecast beside its target and browser full screen.</summary>
public class GameTableScreenTests : ClientTestContext
{
    private readonly FakeBoardRenderer _board;

    public GameTableScreenTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void Render_WithTitleAndConnection_ShowsThemInTheTopStrip()
    {
        var cut = RenderTable(Start(), connection: new GameWatchUpdate(GameWatchStatus.Live));

        cut.Find(".top-bar h1.game-title").TextContent.ShouldBe("Game in progress");
        cut.Find(".top-bar .connection").TextContent.ShouldBe("Live");
    }

    [Fact]
    public void SelectionCollapse_Clicked_ShowsOnlyTheHeadingUntilAnotherSelection()
    {
        var cut = RenderTable(Create([Soldier(1, You, At(2, 2)), Settler(2, You, At(4, 3))]));
        _board.Click(At(2, 2));

        cut.Find(".selection-collapse").Click();

        cut.FindAll(".selection-card .unit-panel").ShouldBeEmpty();
        cut.Find(".selection-summary").TextContent.Trim().ShouldBe("Your Soldier");
        cut.Find(".selection-collapse").GetAttribute("aria-expanded").ShouldBe("false");

        _board.Click(At(4, 3));

        cut.Find(".selection-card .unit-panel .unit-name").TextContent.Trim().ShouldBe("Your Settler");
    }

    [Fact]
    public void SelectedUnit_HasItsActionsInsideTheSelectionCard()
    {
        var cut = RenderTable(Start());

        _board.Click(At(2, 2));

        cut.FindAll(".hud-selection .selection-card .action-bar button.found-city").Count.ShouldBe(1);
    }

    [Fact]
    public void Attack_Cancelled_RemovesTheAnchoredForecast()
    {
        var cut = RenderTable(AdjacentSoldiers());
        _board.Click(At(3, 2));
        _board.Click(At(4, 2));

        cut.Find("button.combat-cancel").Click();

        cut.FindAll(".board-anchor").ShouldBeEmpty();
    }

    [Fact]
    public void Attack_ClickEnemy_AnchorsTheForecastToTheTargetAndLetsGoOnCancel()
    {
        var cut = RenderTable(AdjacentSoldiers());

        _board.Click(At(3, 2));
        _board.Click(At(4, 2));

        cut.WaitForAssertion(() => _board.Calls("anchor").Count.ShouldBe(1));
        _board.Calls("anchor").Single().Arguments.Skip(2).ShouldBe([At(4, 2).Q, At(4, 2).R]);
        cut.Find(".hud-board .board-frame .board-anchor .forecast-card .combat-forecast").ShouldNotBeNull();

        cut.Find("button.combat-cancel").Click();

        cut.WaitForAssertion(() => _board.Calls("unanchor").Count.ShouldBe(1));
    }

    [Fact]
    public void FullScreen_BrowserWithoutIt_HasNoButton()
    {
        var cut = RenderTable(Start());

        cut.FindAll("button.full-screen").ShouldBeEmpty();
    }

    [Fact]
    public void FullScreen_Clicked_AsksTheBrowserForTheScreen()
    {
        var module = JSInterop.SetupModule(GameScreenInteropService.ModulePath);
        module.Setup<bool>("watch", _ => true).SetResult(true);
        module.SetupVoid("enter", _ => true).SetVoidResult();
        var cut = RenderTable(Start());

        cut.WaitForElement("button.full-screen").Click();

        module.Invocations["enter"].Count.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_StopsFollowingTheScreen()
    {
        var module = JSInterop.SetupModule(GameScreenInteropService.ModulePath);
        module.Setup<bool>("watch", _ => true).SetResult(true);
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        var cut = RenderTable(Start());
        cut.WaitForElement("button.full-screen");

        await DisposeComponentsAsync();

        module.Invocations["unwatch"].Count.ShouldBe(1);
    }

    private static PlayerView AdjacentSoldiers() => Create([Settler(1, You, At(1, 1)), Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);

    private IRenderedComponent<GameTable> RenderTable(PlayerView view, GameWatchUpdate? connection = null) =>
        Render<GameTable>(parameters => parameters
            .Add(static table => table.Game, TestGames.Started(view: view))
            .Add(static table => table.Title, "Game in progress")
            .Add(static table => table.Connection, connection));

    private static readonly PlayerId Third = new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000c"));

    [Fact]
    public void Render_TwoPlayers_HasNoPlayerStrip()
    {
        var cut = RenderTable(Start());

        cut.FindAll(".player-strip").ShouldBeEmpty();
    }

    [Fact]
    public void Render_ThreePlayers_ListsThemInTurnOrderAndSaysWhoseTurnItIs()
    {
        var cut = RenderTable(ThreePlayers(PlayerStatus.Active, PlayerStatus.Resigned) with { CurrentPlayer = Opponent });

        cut.FindAll(".player-strip-item").Select(static item => item.TextContent.Trim()).ShouldBe(["You", "Seat 2 (to move)", "Seat 3 (resigned)"]);
        cut.Find(".player-strip-item.current").TextContent.ShouldContain("Seat 2");
        cut.Find(".turn").TextContent.ShouldContain("seat 2's turn");
        cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Resign_WithTwoOthersStillPlaying_SaysTheGameGoesOn()
    {
        var cut = RenderTable(ThreePlayers(PlayerStatus.Active, PlayerStatus.Active));

        cut.Find("button.resign").Click();

        cut.Find(".confirm-text").TextContent.ShouldBe("Resign the game? It goes on for the others.");
    }

    [Fact]
    public void Render_YouResignedWhileTheOthersPlayOn_ShowsYourResultWithoutTurnActions()
    {
        var cut = RenderTable(ThreePlayers(PlayerStatus.Resigned, PlayerStatus.Active) with { CurrentPlayer = Opponent });

        cut.FindAll("button.end-turn, button.resign").ShouldBeEmpty();
        cut.Find(".game-over .result").TextContent.ShouldBe("You resigned. The game goes on for the others.");
    }

    [Fact]
    public void Render_SimultaneousRound_EveryoneMayActAndTheStripSaysWhoEnded()
    {
        var view = ThreePlayers(PlayerStatus.Active, PlayerStatus.Active) with
        {
            TurnMode = TurnMode.Simultaneous,
            Players = [new PlayerSummary(You, PlayerStatus.Active), new PlayerSummary(Opponent, PlayerStatus.Active, EndedTurn: true), new PlayerSummary(Third, PlayerStatus.Active)],
        };

        var cut = RenderTable(view);

        cut.FindAll(".player-strip-item").Select(static item => item.TextContent.Trim()).ShouldBe(["You", "Seat 2 (ended)", "Seat 3"]);
        cut.Find(".turn").TextContent.ShouldContain("1 of 2 others have ended");
        cut.Find("button.end-turn").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Click_UndoEndTurnAfterEndingASimultaneousTurn_SendsResumeTurn()
    {
        var view = Start() with
        {
            TurnMode = TurnMode.Simultaneous,
            Players = [new PlayerSummary(You, PlayerStatus.Active, EndedTurn: true), new PlayerSummary(Opponent, PlayerStatus.Active)],
        };
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started(view: view)));
        var cut = RenderTable(view);
        cut.Find(".turn").TextContent.ShouldContain("waiting for your opponent");
        cut.FindAll("button.end-turn").ShouldBeEmpty();

        cut.Find("button.resume-turn").Click();

        cut.WaitForAssertion(() => Api.RequestsTo(TestGames.CommandsPath()).Single().Body!.ShouldContain("\"type\":\"resume_turn\""));
    }

    private static PlayerView ThreePlayers(PlayerStatus you, PlayerStatus third) => Start() with
    {
        Players = [new PlayerSummary(You, you), new PlayerSummary(Opponent, PlayerStatus.Active), new PlayerSummary(Third, third)],
    };
}
