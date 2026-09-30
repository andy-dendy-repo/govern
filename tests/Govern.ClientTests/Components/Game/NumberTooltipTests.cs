using Govern.Client.Components.Game;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.Web;

namespace Govern.ClientTests.Components.Game;

public class NumberTooltipTests : ClientTestContext
{
    [Fact]
    public void Render_Closed_ShowsTheNumberWithTheDetailAsTitleOnly()
    {
        var cut = RenderTooltip();

        cut.Find(".number").TextContent.ShouldBe("3");
        cut.Find(".number-tooltip").GetAttribute("title").ShouldBe("Three from the ruleset.");
        cut.FindAll(".tooltip-text").ShouldBeEmpty();
        cut.Find("button.tooltip-toggle").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public void Click_InfoButton_TogglesTheDetail()
    {
        var cut = RenderTooltip();

        cut.Find("button.tooltip-toggle").Click();
        var detail = cut.Find(".tooltip-text");
        detail.TextContent.ShouldBe("Three from the ruleset.");
        cut.Find("button.tooltip-toggle").GetAttribute("aria-expanded").ShouldBe("true");
        cut.Find("button.tooltip-toggle").GetAttribute("aria-controls").ShouldBe(detail.Id);
        cut.Find("button.tooltip-toggle").Click();

        cut.FindAll(".tooltip-text").ShouldBeEmpty();
    }

    [Fact]
    public void Escape_Open_ClosesTheDetail()
    {
        var cut = RenderTooltip();
        cut.Find("button.tooltip-toggle").Click();

        cut.Find(".number-tooltip").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".tooltip-text").ShouldBeEmpty();
    }

    private IRenderedComponent<NumberTooltip> RenderTooltip() =>
        Render<NumberTooltip>(parameters => parameters
            .Add(static tooltip => tooltip.Text, "Three from the ruleset.")
            .AddChildContent("3"));
}
