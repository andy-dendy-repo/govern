using Govern.Client.Features.Games.Models;
using Govern.Core.Map;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds the <see cref="BoardScene"/> the 3D board draws from a player's view. Positions come from <see cref="HexLayout"/>,
/// scaled so a hex's corner is 1 unit from its centre, so the 2D and 3D boards place tiles alike.
/// </summary>
public static class BoardSceneBuilder
{
    // Hexes of room left around the player's units and cities when the board first opens.
    private const int StartMargin = 3;

    /// <summary>Builds the scene of a view.</summary>
    /// <param name="view">The player's view.</param>
    /// <param name="maxHealth">The ruleset's full health, for the units' health bars; 0 draws none.</param>
    /// <returns>The scene.</returns>
    public static BoardScene Build(PlayerView view, int maxHealth = 0)
    {
        ArgumentNullException.ThrowIfNull(view);
        var tiles = new SceneTile[view.Tiles.Length];
        for (var index = 0; index < tiles.Length; index++)
        {
            tiles[index] = Tile(view.CoordAt(index), view.Tiles[index]);
        }

        var map = Scale(HexLayout.Bounds(view.Width, view.Height));
        return new BoardScene(
            tiles,
            map,
            StartBounds(view) ?? map,
            SceneLayerBuilder.Rivers(view),
            SceneLayerBuilder.Bridges(view),
            SceneLayerBuilder.Features(view),
            SceneLayerBuilder.Borders(view),
            ScenePieceBuilder.Pieces(view, maxHealth));
    }

    /// <summary>The centre of a tile in board units.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The centre's x and depth.</returns>
    public static (double X, double Z) Center(HexCoord coord)
    {
        var (x, y) = HexLayout.Center(coord);
        return (x / HexLayout.Size, y / HexLayout.Size);
    }

    private static SceneTile Tile(HexCoord coord, TileView tile)
    {
        var (x, z) = Center(coord);
        var relief = TerrainReliefs.For(tile);
        var visibility = tile.Visibility switch
        {
            TileVisibility.Visible => "visible",
            TileVisibility.Explored => "explored",
            _ => "unexplored",
        };
        return new SceneTile(coord.Q, coord.R, x, z, tile.Terrain?.Id, visibility, relief.Height, relief.Shape.ToString().ToLowerInvariant());
    }

    private static BoardRect? StartBounds(PlayerView view)
    {
        HexCoord[] own =
        [
            .. view.Units.Where(unit => unit.Owner == view.Viewer).Select(static unit => unit.Position),
            .. view.Cities.Where(city => city.Owner == view.Viewer).Select(static city => city.Position),
        ];
        return own.Length > 0 ? Scale(HexLayout.Around(own, StartMargin)) : null;
    }

    private static BoardRect Scale(BoardRect rect) =>
        new(rect.Left / HexLayout.Size, rect.Top / HexLayout.Size, rect.Width / HexLayout.Size, rect.Height / HexLayout.Size);
}
