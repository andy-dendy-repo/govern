using Govern.Core.Hexes;

namespace Govern.UnitTests.Core.Hexes;

public class HexCoordTests
{
    [Fact]
    public void Neighbors_Always_ReturnsSixDistinctTilesOneStepAway()
    {
        var center = new HexCoord(2, -1);

        var neighbors = center.Neighbors().ToList();

        neighbors.Count.ShouldBe(6);
        neighbors.Distinct().Count().ShouldBe(6);
        neighbors.ShouldAllBe(neighbor => center.DistanceTo(neighbor) == 1);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 3, -3, 3)]
    [InlineData(0, 0, 2, 1, 3)]
    [InlineData(-2, 4, 1, 1, 3)]
    public void DistanceTo_KnownPairs_ReturnsHexDistanceBothWays(int q1, int r1, int q2, int r2, int expected)
    {
        var a = new HexCoord(q1, r1);
        var b = new HexCoord(q2, r2);

        a.DistanceTo(b).ShouldBe(expected);
        b.DistanceTo(a).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(0, 1)]
    [InlineData(7, 3)]
    [InlineData(11, 9)]
    public void FromOffset_AnyTile_RoundTripsThroughColumnAndRow(int column, int row)
    {
        var coord = HexCoord.FromOffset(column, row);

        coord.Column.ShouldBe(column);
        coord.Row.ShouldBe(row);
        coord.S.ShouldBe(-coord.Q - coord.R);
    }

    [Fact]
    public void FromOffset_OddRow_IsShiftedRightOfTheRowAbove()
    {
        var oddRowTile = HexCoord.FromOffset(0, 1);

        oddRowTile.DistanceTo(HexCoord.FromOffset(0, 0)).ShouldBe(1);
        oddRowTile.DistanceTo(HexCoord.FromOffset(1, 0)).ShouldBe(1);
        oddRowTile.DistanceTo(HexCoord.FromOffset(0, 2)).ShouldBe(1);
        oddRowTile.DistanceTo(HexCoord.FromOffset(1, 2)).ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(2, 19)]
    [InlineData(3, 37)]
    public void WithinRadius_Radius_ReturnsEveryTileUpToThatDistance(int radius, int expectedCount)
    {
        var center = new HexCoord(3, -2);

        var area = center.WithinRadius(radius).ToList();

        area.Count.ShouldBe(expectedCount);
        area.Distinct().Count().ShouldBe(expectedCount);
        area.ShouldAllBe(coord => center.DistanceTo(coord) <= radius);
    }

    [Fact]
    public void WithinRadius_NegativeRadius_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new HexCoord(0, 0).WithinRadius(-1));
    }
}
