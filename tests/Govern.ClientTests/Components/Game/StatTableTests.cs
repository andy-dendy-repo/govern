using Govern.Client.Components.Game;
using Govern.Client.Models;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class StatTableTests : ClientTestContext
{
    [Fact]
    public void Render_Rows_ShowsIconLabelAndValueForEach()
    {
        var cut = Render<StatTable>(parameters => parameters.Add(static table => table.Rows,
            [new StatRow("ui-movement", "Movement", "1 of 2", Class: "movement"), new StatRow("ui-sight", "Sight", "2", Class: "sight")]));

        cut.FindAll("tr").Count.ShouldBe(2);
        cut.Find("tr.movement th").TextContent.ShouldBe("Movement");
        cut.Find("tr.movement .stat-value").TextContent.Trim().ShouldBe("1 of 2");
        cut.Find("tr.sight use").GetAttribute("href").ShouldBe("#ui-sight");
        cut.FindAll(".number-tooltip").ShouldBeEmpty();
    }

    [Fact]
    public void Render_RowWithDetail_WrapsTheValueInATooltip()
    {
        var cut = Render<StatTable>(parameters => parameters.Add(static table => table.Rows,
            [new StatRow("ui-movement", "Movement", "1 of 2", "One left of two.")]));

        cut.Find(".number-tooltip").GetAttribute("title").ShouldBe("One left of two.");
        cut.Find(".number-tooltip .number").TextContent.ShouldBe("1 of 2");
    }
}
