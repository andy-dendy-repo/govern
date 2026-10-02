using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components.Web;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The game log in its drawer, opened by the top strip's Log button.</summary>
public class GameTableLogTests : ClientTestContext
{
    public GameTableLogTests() => SignIn();

    [Fact]
    public void Log_ShowsRoundsNewestFirstWithTheNewestLineOnTop()
    {
        var view = Create(units: [Soldier(1, You, At(2, 2))]);
        GameLogItem[] log =
        [
            new(1, 1, [new UnitMovedEvent(new UnitId(1), You, SoldierType, [At(2, 2)])]),
            new(2, 1, [new TurnEndedEvent(You, Opponent, 1)]),
            new(3, 2, [new TurnEndedEvent(Opponent, You, 2)]),
        ];

        var cut = RenderTable(view, log);
        cut.Find(".log-toggle").Click();

        cut.FindAll(".game-log-round").Select(static round => round.TextContent).ShouldBe(["Turn 2", "Turn 1"]);
        cut.FindAll(".game-log-line").Select(static line => line.TextContent.Trim()).ShouldBe(
            ["Turn 2: your turn", "Turn 1: Seat 2's turn", "Your Soldier moved"]);
    }

    [Fact]
    public void Log_Empty_SaysNothingHappenedYet()
    {
        var cut = RenderTable(Start(), []);
        cut.Find(".log-toggle").Click();

        cut.Find(".game-log-empty").TextContent.ShouldBe("Nothing has happened yet.");
    }

    [Fact]
    public void LogLine_Clicked_SelectsYourUnitOnItsTile()
    {
        var view = Create(units: [Soldier(1, You, At(2, 2))]);
        var cut = RenderTable(view, [new(1, 1, [new UnitMovedEvent(new UnitId(1), You, SoldierType, [At(2, 2)])])]);
        cut.Find(".log-toggle").Click();

        cut.Find(".game-log-show").Click();

        cut.Find(".unit-panel").ShouldNotBeNull();
    }

    [Fact]
    public void Render_Started_KeepsTheLogDrawerClosed()
    {
        var cut = RenderTable(Start(), [new(1, 1, [new TurnEndedEvent(You, Opponent, 1)])]);

        cut.FindAll(".log-drawer").ShouldBeEmpty();
        cut.FindAll(".log-unread").ShouldBeEmpty("what happened before the screen opened is not new");
        cut.Find(".log-toggle").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void LogClose_Clicked_ClosesTheDrawer()
    {
        var cut = RenderTable(Start(), []);
        cut.Find(".log-toggle").Click();

        cut.Find(".log-close").Click();

        cut.FindAll(".log-drawer").ShouldBeEmpty();
    }

    [Fact]
    public void NewLogEntries_WhileClosed_AreCountedOnTheLogButtonUntilItOpens()
    {
        var view = Start();
        GameLogItem first = new(1, 1, [new TurnEndedEvent(You, Opponent, 1)]);
        var cut = RenderTable(view, [first]);

        cut.Render(parameters => parameters.Add(static table => table.Game,
            TestGames.Started(view: view with { Turn = 2 }) with { Log = [first, new(2, 1, [new TurnEndedEvent(Opponent, You, 2)])] }));

        cut.Find(".log-unread").TextContent.ShouldBe("1");
        cut.Find(".log-toggle").Click();
        cut.FindAll(".log-unread").ShouldBeEmpty();
    }

    [Fact]
    public void Escape_WithTheLogOpenAndAUnitSelected_ClosesTheLogFirst()
    {
        var cut = RenderTable(Create(units: [Soldier(1, You, At(2, 2))]), []);
        cut.Find($"use.tile[data-q='{At(2, 2).Q}'][data-r='{At(2, 2).R}']").Click();
        cut.Find(".log-toggle").Click();

        cut.Find(".game-screen").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".log-drawer").ShouldBeEmpty();
        cut.FindAll(".unit-panel").Count.ShouldBe(1);
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view, IReadOnlyList<GameLogItem> log) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));
}
