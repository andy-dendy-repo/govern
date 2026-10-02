using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
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
}
