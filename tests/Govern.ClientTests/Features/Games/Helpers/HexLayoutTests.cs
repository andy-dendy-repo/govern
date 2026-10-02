using System.Globalization;
using Govern.Client.Features.Games.Helpers;
using Govern.ClientTests.Support;
using Govern.Core.Map;

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

    [Fact]
    public void EdgeEnds_EachOwnedSide_LieOnTheBorderBetweenTheTwoTiles()
    {
        var coord = TestViews.At(2, 2);

        foreach (var side in Enum.GetValues<Govern.Core.Map.HexSide>())
        {
            var edge = new Govern.Core.Map.HexEdge(coord, side);
            var (x1, y1, x2, y2) = HexLayout.EdgeEnds(edge);

            // Both ends are corners of both tiles: one hex size from each centre.
            foreach (var centre in new[] { HexLayout.Center(coord), HexLayout.Center(edge.Neighbor()) })
            {
                Distance(centre, (x1, y1)).ShouldBe(HexLayout.Size, 1e-9);
                Distance(centre, (x2, y2)).ShouldBe(HexLayout.Size, 1e-9);
            }

            var (mx, my) = HexLayout.EdgeMiddle(edge);
            (mx, my).ShouldBe(((x1 + x2) / 2, (y1 + y2) / 2));
        }

        static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }

    [Fact]
    public void Around_Hexes_SpansTheirCentresWithTheMargin()
    {
        var (a, b) = (HexLayout.Center(Govern.Core.Map.HexCoord.FromOffset(1, 1)), HexLayout.Center(Govern.Core.Map.HexCoord.FromOffset(4, 3)));

        var bounds = HexLayout.Around([Govern.Core.Map.HexCoord.FromOffset(4, 3), Govern.Core.Map.HexCoord.FromOffset(1, 1)], 2);

        bounds.Left.ShouldBe(a.X - (2 * HexLayout.Width), 1e-9);
        bounds.Top.ShouldBe(a.Y - (3 * HexLayout.Size), 1e-9);
        (bounds.Left + bounds.Width).ShouldBe(b.X + (2 * HexLayout.Width), 1e-9);
        (bounds.Top + bounds.Height).ShouldBe(b.Y + (3 * HexLayout.Size), 1e-9);
    }

    [Fact]
    public void Around_NoHexes_Throws()
    {
        Should.Throw<ArgumentException>(() => HexLayout.Around([], 1));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 2)]
    [InlineData(-2, 5)]
    [InlineData(7, -4)]
    public void Nearest_PointsAroundACentre_GiveThatHex(int q, int r)
    {
        var coord = new HexCoord(q, r);
        var (x, y) = HexLayout.Center(coord);

        HexLayout.Nearest(x, y).ShouldBe(coord);
        HexLayout.Nearest(x + (HexLayout.Width * 0.45), y).ShouldBe(coord);
        HexLayout.Nearest(x, y - (HexLayout.Size * 0.8)).ShouldBe(coord);
        HexLayout.Nearest(x + (HexLayout.Width * 0.55), y).ShouldBe(new HexCoord(q + 1, r));
    }
}
