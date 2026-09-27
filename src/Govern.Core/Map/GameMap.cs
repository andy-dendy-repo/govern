using System.Collections.Immutable;

namespace Govern.Core.Map;

/// <summary>
/// A rectangular hex map in the odd-r offset layout. <see cref="Tiles"/> is stored row by row.
/// </summary>
/// <param name="Width">Number of columns.</param>
/// <param name="Height">Number of rows.</param>
/// <param name="Tiles">All tiles, row-major: index <c>row * Width + column</c>.</param>
public sealed record GameMap(int Width, int Height, ImmutableArray<Tile> Tiles)
{
    /// <summary>Smallest allowed width and height.</summary>
    public const int MinSize = 8;

    /// <summary>Largest allowed width and height.</summary>
    public const int MaxSize = 40;

    /// <summary>Creates a map covered with plains.</summary>
    /// <param name="width">Number of columns, <see cref="MinSize"/> to <see cref="MaxSize"/>.</param>
    /// <param name="height">Number of rows, <see cref="MinSize"/> to <see cref="MaxSize"/>.</param>
    /// <returns>The new map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is out of range.</exception>
    public static GameMap Create(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, MinSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSize);

        var tiles = ImmutableArray.CreateBuilder<Tile>(width * height);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                tiles.Add(new Tile(HexCoord.FromOffset(column, row), TerrainType.Plains));
            }
        }

        return new GameMap(width, height, tiles.MoveToImmutable());
    }

    /// <summary>Whether the coordinate lies on this map.</summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <returns><see langword="true"/> when the tile exists.</returns>
    public bool Contains(HexCoord coord) =>
        coord.Row >= 0 && coord.Row < Height && coord.Column >= 0 && coord.Column < Width;

    /// <summary>Returns the tile at a coordinate.</summary>
    /// <param name="coord">A coordinate on the map.</param>
    /// <returns>The tile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate is outside the map.</exception>
    public Tile TileAt(HexCoord coord)
    {
        if (!Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The coordinate is outside the map.");
        }

        return Tiles[(coord.Row * Width) + coord.Column];
    }

    /// <summary>Whether a land unit may enter the tile, ignoring units and cities.</summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <returns><see langword="true"/> when the tile exists and its terrain is passable.</returns>
    public bool IsPassable(HexCoord coord) => Contains(coord) && TerrainCatalog.Get(TileAt(coord).Terrain).IsPassable;
}
