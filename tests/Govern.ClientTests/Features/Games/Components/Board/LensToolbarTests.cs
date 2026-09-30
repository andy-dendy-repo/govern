using Govern.Client.Features.Games.Components.Board;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Components.Board;

public class LensToolbarTests : ClientTestContext
{
    [Fact]
    public void Render_NoGameScreen_RendersNothing()
    {
        var cut = Render<LensToolbar>();

        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Render_GameScreen_ShowsOneKeyboardButtonPerLens()
    {
        var cut = Render<LensToolbar>(parameters => parameters.AddCascadingValue(new GameUiService()));

        var button = cut.Find(".lens-toolbar[role=toolbar] button[type=button][data-lens=movement-cost]");
        button.TextContent.ShouldContain("Movement cost");
        button.GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void ActiveLens_ChangedElsewhere_PressesTheButton()
    {
        var ui = new GameUiService();
        var cut = Render<LensToolbar>(parameters => parameters.AddCascadingValue(ui));

        ui.ToggleLens(Services.GetServices<IMapLens>().Single());

        cut.WaitForAssertion(() => cut.Find("button[data-lens=movement-cost]").GetAttribute("aria-pressed").ShouldBe("true"));
    }
}
