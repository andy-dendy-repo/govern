using Microsoft.Extensions.DependencyInjection;
using Govern.Client.Layout;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;

namespace Govern.ClientTests.Layout;

public class GameLayoutTests : ClientTestContext
{
    [Fact]
    public void Render_WithBody_RendersTheBodyWithoutTheSidebar()
    {
        var cut = Render<GameLayout>(parameters => parameters.Add(static layout => layout.Body, "<p id=\"body\">Game</p>"));

        cut.Find(".rz-layout main.game-main #body").TextContent.ShouldBe("Game");
        cut.FindAll(".rz-sidebar").ShouldBeEmpty();
    }

    [Fact]
    public void BackToGames_Clicked_GoesToTheGames()
    {
        SignIn("player@example.com");
        var cut = Render<GameLayout>();

        cut.Find(".back-to-games").Click();

        Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith("/games");
        cut.Find(".rz-header .user-name").TextContent.ShouldBe("player@example.com");
    }
}
