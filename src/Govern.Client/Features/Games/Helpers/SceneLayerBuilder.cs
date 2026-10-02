using Govern.Client.Features.Games.Models;
using Govern.Client.Helpers;
using Govern.Core.Map;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds the layers of a <see cref="BoardScene"/> above the tiles: rivers, bridges, improvements and buildings, and territory
/// borders. It shows known rivers on
/// visible and explored tiles, the rest only in sight, plus the buildings the player's cities are producing.
/// </summary>
public static class SceneLayerBuilder
{
    // Where an improvement or building stands: this far towards the tile's upper left (on both axes), in board units, so the
    // middle stays free for a unit or city.
    private const double FeatureOffset = 0.36;

    // How far towards the owned tile's centre a border is moved, as a share of the way.
    private const double BorderInset = 0.12;

    // How far a bridge reaches into each tile, as a share of the way from the edge to the tile's centre: far enough that the
    // deck rests on both banks.
    private const double BridgeReach = 0.45;

    /// <summary>The known rivers, one per river edge.</summary>
    /// <param name="view">The player's view.</param>
    /// <returns>The rivers.</returns>
    public static IReadOnlyList<SceneRiver> Rivers(PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var rivers = new List<SceneRiver>();
        for (var index = 0; index < view.Tiles.Length; index++)
        {
            if (view.Tiles[index] is not { Rivers: { } kinds } tile)
            {
                continue;
            }

            foreach (var side in Enum.GetValues<HexSide>())
            {
                if (kinds.On(side) is not { } river)
                {
                    continue;
                }

                var edge = new HexEdge(view.CoordAt(index), side);
                var (x1, z1, x2, z2) = Scale(HexLayout.EdgeEnds(edge));
                var relief = RiverReliefs.For(river);
                var visibility = tile.Visibility == TileVisibility.Visible ? "visible" : "explored";
                rivers.Add(new SceneRiver(x1, z1, x2, z2, LowerSurface(view, edge), relief.Width, relief.Deep, visibility));
            }
        }

        return rivers;
    }

    /// <summary>The bridges in sight, each from one tile across its edge to the other.</summary>
    /// <param name="view">The player's view.</param>
    /// <returns>The bridges.</returns>
    public static IReadOnlyList<SceneBridge> Bridges(PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var bridges = new List<SceneBridge>();
        for (var index = 0; index < view.Tiles.Length; index++)
        {
            if (view.Tiles[index] is not { Bridges: { } sides } tile)
            {
                continue;
            }

            foreach (var side in Enum.GetValues<HexSide>().Where(sides.On))
            {
                var edge = new HexEdge(view.CoordAt(index), side);
                var (ax, az) = BoardSceneBuilder.Center(edge.Owner);
                var (bx, bz) = BoardSceneBuilder.Center(edge.Neighbor());
                var (mx, mz) = ((ax + bx) / 2, (az + bz) / 2);
                var far = view.Contains(edge.Neighbor()) ? TerrainReliefs.Surface(view.TileAt(edge.Neighbor())) : TerrainReliefs.Surface(tile);
                bridges.Add(new SceneBridge(
                    mx + ((ax - mx) * BridgeReach), mz + ((az - mz) * BridgeReach), TerrainReliefs.Surface(tile),
                    mx + ((bx - mx) * BridgeReach), mz + ((bz - mz) * BridgeReach), far));
            }
        }

        return bridges;
    }

    /// <summary>
    /// The improvements and buildings in sight (a building in its territory owner's seat), then the buildings the player's cities
    /// are producing, on the tiles reserved for them, planned.
    /// </summary>
    /// <param name="view">The player's view.</param>
    /// <returns>The features.</returns>
    public static IReadOnlyList<SceneFeature> Features(PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var features = new List<SceneFeature>();
        for (var index = 0; index < view.Tiles.Length; index++)
        {
            var tile = view.Tiles[index];
            if (tile.Improvement is { } improvement)
            {
                features.Add(Feature(view.CoordAt(index), tile, FeatureModels.For(improvement), 0, planned: false));
            }

            if (tile is { Building: { } building, TerritoryOwner: { } owner })
            {
                features.Add(Feature(view.CoordAt(index), tile, FeatureModels.For(building), Seats.Number(view, owner), planned: false));
            }
        }

        foreach (var city in view.Cities)
        {
            if (city is { ProducingBuilding: { } building, ProducingBuildingSite: { } site } && view.Contains(site))
            {
                features.Add(Feature(site, view.TileAt(site), FeatureModels.For(building), Seats.Number(view, city.Owner), planned: true));
            }
        }

        return features;
    }

    /// <summary>
    /// The territory borders: every edge of an owned tile whose neighbour has another owner, no owner or is off the map, set
    /// slightly inside the owned tile.
    /// </summary>
    /// <param name="view">The player's view.</param>
    /// <returns>The borders.</returns>
    public static IReadOnlyList<SceneBorder> Borders(PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var borders = new List<SceneBorder>();
        for (var index = 0; index < view.Tiles.Length; index++)
        {
            if (view.Tiles[index] is not { TerritoryOwner: { } owner } tile)
            {
                continue;
            }

            var coord = view.CoordAt(index);
            var (cx, cz) = BoardSceneBuilder.Center(coord);
            foreach (var neighbour in coord.Neighbors())
            {
                if (view.Contains(neighbour) && view.TileAt(neighbour).TerritoryOwner == owner)
                {
                    continue;
                }

                var (x1, z1, x2, z2) = Scale(HexLayout.EdgeEnds(HexEdge.Between(coord, neighbour)!.Value));
                borders.Add(new SceneBorder(
                    x1 + ((cx - x1) * BorderInset), z1 + ((cz - z1) * BorderInset),
                    x2 + ((cx - x2) * BorderInset), z2 + ((cz - z2) * BorderInset),
                    TerrainReliefs.Surface(tile), Seats.Number(view, owner)));
            }
        }

        return borders;
    }

    private static SceneFeature Feature(HexCoord coord, TileView tile, Enums.FeatureModel model, int seat, bool planned)
    {
        var (x, z) = BoardSceneBuilder.Center(coord);
        return new SceneFeature(
            coord.Q, coord.R, x - FeatureOffset, z - FeatureOffset, TerrainReliefs.Surface(tile), model.ToString().ToLowerInvariant(), seat, planned);
    }

    // The lower of the known surfaces on both sides of an edge; an edge on the map's rim or next to an unexplored tile (which
    // lies flat under the mist) takes the known side's.
    private static double LowerSurface(PlayerView view, HexEdge edge)
    {
        var own = TerrainReliefs.Surface(view.TileAt(edge.Owner));
        return view.Contains(edge.Neighbor()) && view.TileAt(edge.Neighbor()) is { Terrain: not null } neighbour
            ? Math.Min(own, TerrainReliefs.Surface(neighbour))
            : own;
    }

    private static (double X1, double Z1, double X2, double Z2) Scale((double X1, double Y1, double X2, double Y2) ends) =>
        (ends.X1 / HexLayout.Size, ends.Y1 / HexLayout.Size, ends.X2 / HexLayout.Size, ends.Y2 / HexLayout.Size);
}
