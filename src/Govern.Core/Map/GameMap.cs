using System.Collections.Immutable;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// A rectangular hex map in the odd-r offset layout. <see cref="Tiles"/> is stored row by row.
/// </summary>
/// <param name="Width">Number of columns.</param>
/// <param name="Height">Number of rows.</param>
/// <param name="Tiles">All tiles, row-major: index <c>row * Width + column</c>.</param>
public sealed record GameMap(int Width, int Height, ImmutableArray<Tile> Tiles)
{
    /// <summary>Creates a map covered with one terrain. The ruleset's size limits are checked by <see cref="State.GameFactory"/>.</summary>
    /// <param name="width">Number of columns, at least 1.</param>
    /// <param name="height">Number of rows, at least 1.</param>
    /// <param name="terrain">The terrain of every tile.</param>
    /// <returns>The new map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is below 1.</exception>
    public static GameMap Create(int width, int height, TerrainType terrain)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var tiles = ImmutableArray.CreateBuilder<Tile>(width * height);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                tiles.Add(new Tile(HexCoord.FromOffset(column, row), terrain));
            }
        }

        return new GameMap(width, height, tiles.MoveToImmutable());
    }

    /// <summary>
    /// The tile opposite a coordinate through the map's centre (a 180° turn): column <c>Width − 1 − column</c>, row
    /// <c>Height − 1 − row</c>. The map generator lays terrain on both, so the two halves of a map are the same. On a map
    /// with an even height this keeps neighbours neighbours.
    /// </summary>
    /// <param name="coord">A coordinate on the map.</param>
    /// <returns>The opposite coordinate.</returns>
    public HexCoord MirrorOf(HexCoord coord) => HexCoord.FromOffset(Width - 1 - coord.Column, Height - 1 - coord.Row);

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
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="coord">The coordinate to test.</param>
    /// <returns><see langword="true"/> when the tile exists and its terrain is passable.</returns>
    public bool IsPassable(Ruleset ruleset, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return Contains(coord) && ruleset.Terrain(TileAt(coord).Terrain).IsPassable;
    }

    /// <summary>The river on the edge between two neighbouring tiles.</summary>
    /// <param name="a">One tile.</param>
    /// <param name="b">The other tile.</param>
    /// <returns>The river, or <see langword="null"/> when there is none, the tiles are not neighbours or the edge's owner is off the map.</returns>
    public RiverType? RiverBetween(HexCoord a, HexCoord b) =>
        HexEdge.Between(a, b) is { } edge && Contains(edge.Owner) ? TileAt(edge.Owner).Rivers?.On(edge.Side) : null;

    /// <summary>Whether the edge between two neighbouring tiles has a bridge.</summary>
    /// <param name="a">One tile.</param>
    /// <param name="b">The other tile.</param>
    /// <returns><see langword="true"/> when the edge is bridged.</returns>
    public bool BridgeBetween(HexCoord a, HexCoord b) =>
        HexEdge.Between(a, b) is { } edge && Contains(edge.Owner) && TileAt(edge.Owner).Bridges?.On(edge.Side) == true;

    /// <summary>
    /// The river a unit crosses stepping or attacking from one tile to its neighbour: the river on the edge between them, unless
    /// a bridge spans it (a bridged edge is crossed as if it had no river).
    /// </summary>
    /// <param name="a">The tile the unit is on.</param>
    /// <param name="b">The neighbouring tile.</param>
    /// <returns>The river crossed, or <see langword="null"/> when there is none or it is bridged.</returns>
    public RiverType? CrossedRiverBetween(HexCoord a, HexCoord b) => BridgeBetween(a, b) ? null : RiverBetween(a, b);

    /// <summary>The rivers on the six edges of a tile, bridged or not, each edge at most once.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The river of every edge that has one.</returns>
    public IEnumerable<RiverType> RiversAround(HexCoord coord) =>
        coord.Neighbors().Select(neighbor => RiverBetween(coord, neighbor)).OfType<RiverType>();

    // A copy with a river on one edge; the owner tile must be on the map.
    internal GameMap WithRiver(HexEdge edge, RiverType river)
    {
        var tile = TileAt(edge.Owner);
        var rivers = (tile.Rivers ?? new TileRivers()).With(edge.Side, river);
        return WithTile(tile with { Rivers = rivers });
    }

    // A copy with a bridge on one edge; the owner tile must be on the map.
    internal GameMap WithBridge(HexEdge edge)
    {
        var tile = TileAt(edge.Owner);
        return WithTile(tile with { Bridges = (tile.Bridges ?? new TileBridges()).With(edge.Side) });
    }

    // A copy with a tile replaced (the tile at the same coordinate).
    internal GameMap WithTile(Tile tile) => this with { Tiles = Tiles.SetItem((tile.Coord.Row * Width) + tile.Coord.Column, tile) };
}
