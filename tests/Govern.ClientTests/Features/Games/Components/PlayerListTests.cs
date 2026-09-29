using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Games.Components;

public class PlayerListTests : ClientTestContext
{
    [Fact]
    public void Render_OneSeat_ShowsTheCallerAndAnEmptySeat()
    {
        var cut = Render<PlayerList>(parameters => parameters.Add(static list => list.Players, TestGames.CreatorLobby().Players));

        Texts(cut).ShouldBe(["Player 1 (you, creator)", "Waiting for a second player…"]);
    }

    [Fact]
    public void Render_JoinerView_MarksTheJoinerAsYou()
    {
        var cut = Render<PlayerList>(parameters => parameters.Add(static list => list.Players, TestGames.JoinerLobby().Players));

        Texts(cut).ShouldBe(["Player 1 (creator)", "Player 2 (you)"]);
    }

    private static string[] Texts(IRenderedComponent<PlayerList> cut) =>
        [.. cut.FindAll(".player").Select(static player => player.TextContent.Trim())];
}
