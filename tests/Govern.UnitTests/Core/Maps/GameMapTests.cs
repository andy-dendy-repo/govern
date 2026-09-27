using Govern.Core.Hexes;
using Govern.Core.Maps;

namespace Govern.UnitTests.Core.Maps;

public class GameMapTests
{
    [Fact]
    public void Create_ValidSize_StoresPlainsRowByRow()
    {
        var map = GameMap.Create(10, 8);

        map.Tiles.Length.ShouldBe(80);
        map.Tiles.ShouldAllBe(tile => tile.Terrain == TerrainType.Plains);
        map.Tiles[(3 * 10) + 7].Coord.ShouldBe(HexCoord.FromOffset(7, 3));
        map.TileAt(HexCoord.FromOffset(9, 7)).ShouldBe(map.Tiles[^1]);
    }

    [Theory]
    [InlineData(GameMap.MinSize - 1, 10)]
    [InlineData(GameMap.MaxSize + 1, 10)]
    [InlineData(10, GameMap.MinSize - 1)]
    [InlineData(10, GameMap.MaxSize + 1)]
    public void Create_SizeOutOfRange_Throws(int width, int height)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GameMap.Create(width, height));
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
        var map = GameMap.Create(10, 8);

        map.Contains(HexCoord.FromOffset(column, row)).ShouldBe(expected);
        map.IsPassable(HexCoord.FromOffset(column, row)).ShouldBe(expected);
    }

    [Fact]
    public void TileAt_OutsideMap_Throws()
    {
        var map = GameMap.Create(10, 8);

        Should.Throw<ArgumentOutOfRangeException>(() => map.TileAt(HexCoord.FromOffset(10, 0)));
    }
}
