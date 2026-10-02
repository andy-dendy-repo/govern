using System.Globalization;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardViewportTests
{
    [Fact]
    public void Fit_Map_CentresItAsLargeAsItFits()
    {
        var bounds = new BoardRect(-10, -20, 100, 50);

        var viewport = BoardViewport.Fit(bounds, 400, 300);

        viewport.Scale.ShouldBe(3);
        ((40 * viewport.Scale) + viewport.X).ShouldBe(200, 1e-9);
        ((5 * viewport.Scale) + viewport.Y).ShouldBe(150, 1e-9);
    }

    [Fact]
    public void Fit_HugeMap_ClampsTheZoom()
    {
        BoardViewport.Fit(new BoardRect(0, 0, 10_000, 10_000), 400, 300).Scale.ShouldBe(BoardViewport.MinScale);
    }

    [Fact]
    public void Fit_UnmeasuredElement_KeepsAUsableZoom()
    {
        BoardViewport.Fit(new BoardRect(0, 0, 0, 0), 0, 0).Scale.ShouldBe(1);
    }

    [Fact]
    public void Pan_MovesTheOffset()
    {
        new BoardViewport(2, 10, 20).Pan(5, -7).ShouldBe(new BoardViewport(2, 15, 13));
    }

    [Fact]
    public void ZoomAt_Anchor_StaysUnderThePointer()
    {
        var viewport = new BoardViewport(1.5, 10, 20);
        var (boardX, boardY) = ((100 - viewport.X) / viewport.Scale, (80 - viewport.Y) / viewport.Scale);

        var zoomed = viewport.ZoomAt(100, 80, 1.2);

        zoomed.Scale.ShouldBe(1.8, 1e-9);
        ((boardX * zoomed.Scale) + zoomed.X).ShouldBe(100, 1e-9);
        ((boardY * zoomed.Scale) + zoomed.Y).ShouldBe(80, 1e-9);
    }

    [Theory]
    [InlineData(10, BoardViewport.MaxScale)]
    [InlineData(0.01, BoardViewport.MinScale)]
    public void ZoomAt_BeyondTheLimits_Clamps(double factor, double expected)
    {
        new BoardViewport(1, 0, 0).ZoomAt(0, 0, factor).Scale.ShouldBe(expected);
    }

    [Fact]
    public void Transform_CommaCulture_UsesDots()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            new BoardViewport(1.25, 10.5, -3).Transform.ShouldBe("translate(10.5 -3) scale(1.25)");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void ToScreen_BoardPoint_AppliesTheZoomAndTheOffset() =>
        new BoardViewport(2, 10, -5).ToScreen(3, 4).ShouldBe((16d, 3d));
}
