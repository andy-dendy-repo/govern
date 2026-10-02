using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds the <see cref="BoardOverlay"/> the board draws over its scene: the active lens's marks and edge badges, then the selection's highlights (the pending target in its own
/// tint), and the selected unit's or city's tile.
/// </summary>
public static class BoardOverlayBuilder
{
    /// <summary>Builds the overlay.</summary>
    /// <param name="view">The player's view.</param>
    /// <param name="selection">The selection.</param>
    /// <param name="highlights">The selection's highlights.</param>
    /// <param name="lens">The active lens, or <see langword="null"/> when none is on.</param>
    /// <returns>The overlay; <see cref="BoardOverlay.None"/> when there is nothing to draw.</returns>
    public static BoardOverlay Build(PlayerView view, BoardSelection selection, BoardHighlights highlights, IMapLens? lens)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(highlights);
        var tiles = new List<OverlayTile>();
        var edges = new List<OverlayEdge>();
        if (lens is not null)
        {
            AddLens(view, lens, tiles, edges);
        }

        AddHighlights(view, highlights.Reachable, "--gv-overlay-reachable", tiles);
        AddHighlights(view, highlights.Attackable.Where(coord => coord != selection.Target), "--gv-overlay-attackable", tiles);
        AddHighlights(view, highlights.Attackable.Where(coord => coord == selection.Target), "--gv-overlay-target", tiles);
        AddHighlights(view, highlights.Bridgeable, "--gv-overlay-bridgeable", tiles);
        AddHighlights(view, highlights.Placeable, "--gv-overlay-placeable", tiles);
        var selected = Selected(view, selection) is { } at && view.Contains(at) ? Tile(view, at, "--gv-selected") : null;
        return tiles.Count == 0 && edges.Count == 0 && selected is null ? BoardOverlay.None : new BoardOverlay(tiles, edges, selected);
    }

    private static void AddLens(PlayerView view, IMapLens lens, List<OverlayTile> tiles, List<OverlayEdge> edges)
    {
        for (var index = 0; index < view.Tiles.Length; index++)
        {
            var coord = view.CoordAt(index);
            if (lens.MarkFor(view, index) is { } mark)
            {
                tiles.Add(Tile(view, coord, $"--gv-lens-{mark.Class}") with { Label = mark.Label });
            }

            foreach (var edgeMark in lens.EdgeMarksFor(view.Tiles[index]))
            {
                var edge = new HexEdge(coord, edgeMark.Side);
                var (x, y) = HexLayout.EdgeMiddle(edge);
                var height = view.Contains(edge.Neighbor())
                    ? Math.Max(TerrainReliefs.Surface(view.Tiles[index]), TerrainReliefs.Surface(view.TileAt(edge.Neighbor())))
                    : TerrainReliefs.Surface(view.Tiles[index]);
                edges.Add(new OverlayEdge(x / HexLayout.Size, y / HexLayout.Size, height, edgeMark.Label, edgeMark.Class));
            }
        }
    }

    private static void AddHighlights(PlayerView view, IEnumerable<HexCoord> coords, string tint, List<OverlayTile> tiles) =>
        tiles.AddRange(coords.Where(view.Contains).OrderBy(static coord => coord.R).ThenBy(static coord => coord.Q).Select(coord => Tile(view, coord, tint)));

    private static HexCoord? Selected(PlayerView view, BoardSelection selection) =>
        selection.Unit is { } unit ? view.Units.FirstOrDefault(candidate => candidate.Id == unit)?.Position
        : selection.City is { } city ? view.Cities.FirstOrDefault(candidate => candidate.Id == city)?.Position
        : null;

    private static OverlayTile Tile(PlayerView view, HexCoord coord, string tint)
    {
        var (x, z) = BoardSceneBuilder.Center(coord);
        return new OverlayTile(x, z, TerrainReliefs.Surface(view.TileAt(coord)), tint);
    }
}
