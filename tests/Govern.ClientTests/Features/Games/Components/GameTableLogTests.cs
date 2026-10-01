using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The game log at the bottom of the side panel.</summary>
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

        cut.FindAll(".game-log-round").Select(static round => round.TextContent).ShouldBe(["Turn 2", "Turn 1"]);
        cut.FindAll(".game-log-line").Select(static line => line.TextContent.Trim()).ShouldBe(
            ["Turn 2: your turn", "Turn 1: Seat 2's turn", "Your Soldier moved"]);
    }

    [Fact]
    public void Log_Empty_SaysNothingHappenedYet()
    {
        var cut = RenderTable(Start(), []);

        cut.Find(".game-log-empty").TextContent.ShouldBe("Nothing has happened yet.");
    }

    [Fact]
    public void LogLine_Clicked_SelectsYourUnitOnItsTile()
    {
        var view = Create(units: [Soldier(1, You, At(2, 2))]);
        var cut = RenderTable(view, [new(1, 1, [new UnitMovedEvent(new UnitId(1), You, SoldierType, [At(2, 2)])])]);

        cut.Find(".game-log-show").Click();

        cut.Find(".unit-panel").ShouldNotBeNull();
    }

    [Fact]
    public void LogToggle_Clicked_CollapsesTheLog()
    {
        var cut = RenderTable(Start(), []);

        cut.Find(".game-log-toggle").Click();

        cut.FindAll(".game-log-empty").ShouldBeEmpty();
        cut.Find(".game-log-toggle").GetAttribute("aria-expanded").ShouldBe("false");
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view, IReadOnlyList<GameLogItem> log) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));
}
