using Govern.Client.Components.Game;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class CostLabelTests : ClientTestContext
{
    [Fact]
    public void Render_Default_ShowsTheProductionIconAndAmount()
    {
        var cut = Render<CostLabel>(parameters => parameters.Add(static label => label.Amount, 4));

        cut.Find(".cost-amount").TextContent.ShouldBe("4");
        cut.Find("use").GetAttribute("href").ShouldBe("#ui-production");
        cut.Find("svg").GetAttribute("aria-label").ShouldBe("production");
        cut.Find(".cost-label").GetAttribute("title").ShouldBe("4 production");
    }
}
