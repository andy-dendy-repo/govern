using Govern.Client.Features.Games.Components.Board;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Games.Components.Board;

public class BoardModeToggleTests : ClientTestContext
{
    [Fact]
    public void Render_OutsideAGameScreen_RendersNothing()
    {
        var cut = Render<BoardModeToggle>();

        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Click_Toggle_SwitchesTheBoardAndItsLabel()
    {
        var ui = new GameUiService();
        var cut = Render<BoardModeToggle>(parameters => parameters.AddCascadingValue(ui));
        cut.Find("button.board-mode").TextContent.Trim().ShouldBe("3D");

        cut.Find("button.board-mode").Click();

        ui.Board3D.ShouldBeTrue();
        cut.Render();
        cut.Find("button.board-mode").TextContent.Trim().ShouldBe("2D");
        cut.Find("button.board-mode").GetAttribute("aria-pressed").ShouldBe("true");
    }
}
