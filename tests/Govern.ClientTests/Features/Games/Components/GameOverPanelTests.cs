using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.State;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

public class GameOverPanelTests : ClientTestContext
{
    [Fact]
    public void Render_YouWon_SaysSoAndHowTheOpponentLeft()
    {
        var cut = RenderPanel(Finished(You, PlayerStatus.Resigned));

        cut.Find(".result").TextContent.ShouldBe("You won!");
        cut.FindAll(".player-result").Select(static line => line.TextContent.Trim()).ShouldBe(["You: won", "Your opponent: resigned"]);
        cut.Find("a.back").GetAttribute("href").ShouldBe("games");
    }

    [Fact]
    public void Render_YouLost_SaysSo()
    {
        var view = Finished(Opponent) with
        {
            Players = [new PlayerSummary(You, PlayerStatus.Eliminated), new PlayerSummary(Opponent, PlayerStatus.Active)],
        };

        var cut = RenderPanel(view);

        cut.Find(".result").TextContent.ShouldBe("You lost.");
        cut.FindAll(".player-result").Select(static line => line.TextContent.Trim()).ShouldBe(["You: eliminated", "Your opponent: won"]);
    }

    [Fact]
    public void Render_NoWinner_SaysNobodyWon()
    {
        RenderPanel(Finished(winner: null)).Find(".result").TextContent.ShouldBe("Nobody won.");
    }

    private IRenderedComponent<GameOverPanel> RenderPanel(PlayerView view) =>
        Render<GameOverPanel>(parameters => parameters.Add(static panel => panel.View, view));
}
