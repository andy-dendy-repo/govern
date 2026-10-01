using Govern.Core.Map;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Map;

public class GameMapTests
{
    [Fact]
    public void Create_ValidSize_StoresPlainsRowByRow()
    {
        var map = GameMap.Create(10, 8, TestRulesets.Plains);

        map.Tiles.Length.ShouldBe(80);
        map.Tiles.ShouldAllBe(tile => tile.Terrain == TestRulesets.Plains);
        map.Tiles[(3 * 10) + 7].Coord.ShouldBe(HexCoord.FromOffset(7, 3));
        map.TileAt(HexCoord.FromOffset(9, 7)).ShouldBe(map.Tiles[^1]);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void Create_EmptyDimension_Throws(int width, int height)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GameMap.Create(width, height, TestRulesets.Plains));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(9, 7, true)]
    [InlineData(-1, 0, false)]
    [InlineData(10, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(0, 8, false)]
    public void Contains_Coordinate_MatchesMapBounds(int column, int row, bool expected)
    {
        var map = GameMap.Create(10, 8, TestRulesets.Plains);

        map.Contains(HexCoord.FromOffset(column, row)).ShouldBe(expected);
        map.IsPassable(TestRulesets.Default, HexCoord.FromOffset(column, row)).ShouldBe(expected);
    }

    [Fact]
    public void TileAt_OutsideMap_Throws()
    {
        var map = GameMap.Create(10, 8, TestRulesets.Plains);

        Should.Throw<ArgumentOutOfRangeException>(() => map.TileAt(HexCoord.FromOffset(10, 0)));
    }

    [Fact]
    public void MirrorOf_EvenHeight_KeepsNeighboursNeighbours()
    {
        var map = GameMap.Create(9, 8, TestRulesets.Plains);

        foreach (var tile in map.Tiles)
        {
            foreach (var neighbour in tile.Coord.Neighbors().Where(map.Contains))
            {
                map.MirrorOf(tile.Coord).DistanceTo(map.MirrorOf(neighbour)).ShouldBe(1);
            }
        }
    }

    [Fact]
    public void MirrorOf_Corner_IsTheOppositeCorner()
    {
        var map = GameMap.Create(9, 8, TestRulesets.Plains);

        map.MirrorOf(HexCoord.FromOffset(0, 0)).ShouldBe(HexCoord.FromOffset(8, 7));
    }

    [Fact]
    public void RiverBetween_EveryDirection_FindsTheEdgeFromEitherSide()
    {
        var state = new GameStateBuilder().WithRiver(TestRulesets.River, ((5, 5), (6, 5))).Build();
        var center = GameStateBuilder.At(5, 5);

        foreach (var neighbor in center.Neighbors())
        {
            var expected = neighbor == GameStateBuilder.At(6, 5) ? TestRulesets.River : (RiverType?)null;
            state.Map.RiverBetween(center, neighbor).ShouldBe(expected);
            state.Map.RiverBetween(neighbor, center).ShouldBe(expected);
        }
    }

    [Fact]
    public void RiverBetween_TilesNotNeighbours_IsNull()
    {
        var state = new GameStateBuilder().WithRiver(TestRulesets.River, ((5, 5), (6, 5))).Build();

        state.Map.RiverBetween(GameStateBuilder.At(5, 5), GameStateBuilder.At(7, 5)).ShouldBeNull();
        state.Map.RiverBetween(GameStateBuilder.At(5, 5), GameStateBuilder.At(5, 5)).ShouldBeNull();
    }

    [Fact]
    public void HexEdge_EachPairOfNeighbours_HasOneOwnerOnTheMapSide()
    {
        var center = GameStateBuilder.At(5, 5);

        foreach (var neighbor in center.Neighbors())
        {
            var edge = HexEdge.Between(center, neighbor)!.Value;
            HexEdge.Between(neighbor, center).ShouldBe(edge);
            new[] { edge.Owner, edge.Neighbor() }.ShouldBe([center, neighbor], ignoreOrder: true);
        }

        HexEdge.Between(center, GameStateBuilder.At(7, 5)).ShouldBeNull();
    }
}
