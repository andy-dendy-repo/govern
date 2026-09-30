using Govern.Client.Components.Game;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class HealthBarTests : ClientTestContext
{
    [Theory]
    [InlineData(100, false)]
    [InlineData(34, false)]
    [InlineData(33, true)]
    [InlineData(1, true)]
    public void Render_Health_ShowsTheNumbersAndMarksLowHealth(int health, bool low)
    {
        var cut = Render<HealthBar>(parameters => parameters
            .Add(static bar => bar.Health, health)
            .Add(static bar => bar.Max, 100));

        cut.Find(".health-text").TextContent.ShouldBe($"{health} of 100");
        cut.Find(".health-bar").ClassList.Contains("low").ShouldBe(low);
        cut.Find("[role=progressbar]").GetAttribute("aria-label").ShouldBe("Health");
    }

    [Fact]
    public void Render_Label_NamesTheBar()
    {
        var cut = Render<HealthBar>(parameters => parameters
            .Add(static bar => bar.Health, 50)
            .Add(static bar => bar.Max, 100)
            .Add(static bar => bar.Label, "Enemy Soldier health"));

        cut.Find("[role=progressbar]").GetAttribute("aria-label").ShouldBe("Enemy Soldier health");
    }
}
