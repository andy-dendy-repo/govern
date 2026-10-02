using Govern.Client.Features.Games.Helpers;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardAnchorTests
{
    [Theory]
    [InlineData(100, 50.25, 800, "--anchor-x: 100px; --anchor-y: 50.3px; --anchor-flip: 0")]
    [InlineData(600, 200, 800, "--anchor-x: 600px; --anchor-y: 200px; --anchor-flip: 1")]
    public void Style_PointOnTheBoard_PlacesTheCardOnTheSideWithMoreRoom(double x, double y, double width, string expected) =>
        BoardAnchor.Style(x, y, width).ShouldBe(expected);
}
