using System.Globalization;
using Govern.Client.Features.Games.Helpers;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Games.Helpers;

public class HexLayoutTests
{
    [Fact]
    public void Center_Neighbours_AreOneHexWidthApart()
    {
        var coord = TestViews.At(2, 2);
        var (x, y) = HexLayout.Center(coord);

        foreach (var neighbour in coord.Neighbors())
        {
            var (nx, ny) = HexLayout.Center(neighbour);
            Math.Sqrt(((nx - x) * (nx - x)) + ((ny - y) * (ny - y))).ShouldBe(HexLayout.Width, 1e-9);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 6)]
    [InlineData(20, 14)]
    public void Bounds_Map_HoldEveryHexAndTouchTheOuterOnes(int width, int height)
    {
        var bounds = HexLayout.Bounds(width, height);
        var centres = Enumerable.Range(0, width * height)
            .Select(index => HexLayout.Center(Govern.Core.Map.HexCoord.FromOffset(index % width, index / width)))
            .ToList();

        centres.Min(static centre => centre.X - (HexLayout.Width / 2)).ShouldBe(bounds.Left, 1e-9);
        centres.Min(static centre => centre.Y - HexLayout.Size).ShouldBe(bounds.Top, 1e-9);
        centres.Max(static centre => centre.X + (HexLayout.Width / 2)).ShouldBe(bounds.Left + bounds.Width, 1e-9);
        centres.Max(static centre => centre.Y + HexLayout.Size).ShouldBe(bounds.Top + bounds.Height, 1e-9);
    }

    [Fact]
    public void Bounds_EmptyMap_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HexLayout.Bounds(0, 3));
    }

    [Fact]
    public void Corners_AreSixPointsOnTheCircle()
    {
        var points = HexLayout.Corners.Split(' ')
            .Select(static point => point.Split(',').Select(static part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray())
            .ToList();

        points.Count.ShouldBe(6);
        points.ShouldAllBe(point => Math.Abs(Math.Sqrt((point[0] * point[0]) + (point[1] * point[1])) - HexLayout.Size) < 0.01);
    }

    [Fact]
    public void Format_CommaCulture_UsesADot()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            HexLayout.Format(1.25).ShouldBe("1.25");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
