using Govern.Client.Components.Game;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class ProgressBarTests : ClientTestContext
{
    [Theory]
    [InlineData(2, 6, 1, "4 turns left")]
    [InlineData(5, 6, 1, "1 turn left")]
    [InlineData(1, 6, 2, "3 turns left")]
    [InlineData(2, 7, 2, "3 turns left")]
    [InlineData(6, 6, 1, "Complete")]
    [InlineData(8, 6, 1, "Complete")]
    [InlineData(2, 6, 0, "Never at this rate")]
    public void Render_Progress_ShowsTheTurnsLeft(int value, int max, int perTurn, string expected)
    {
        var cut = Render<ProgressBar>(parameters => parameters
            .Add(static bar => bar.Value, value)
            .Add(static bar => bar.Max, max)
            .Add(static bar => bar.PerTurn, perTurn)
            .Add(static bar => bar.Label, "Production"));

        cut.Find(".progress-turns").TextContent.ShouldBe(expected);
    }
}
