using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.Client.Helpers;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds the pieces of a <see cref="BoardScene"/>: every city and unit in the view (the view holds only what the player sees),
/// as <c>BoardPieces</c> draws them on the 2D board. A unit on a city's tile stands towards the front of the tile, so both show.
/// </summary>
public static class ScenePieceBuilder
{
    // Where a unit stands on a city's tile: towards the tile's lower right (the front), in board units.
    private const double BesideCity = 0.3;

    /// <summary>The cities, then the units.</summary>
    /// <param name="view">The player's view.</param>
    /// <param name="maxHealth">The ruleset's full health; 0 treats every unit as unhurt.</param>
    /// <returns>The pieces.</returns>
    public static IReadOnlyList<ScenePiece> Pieces(PlayerView view, int maxHealth)
    {
        ArgumentNullException.ThrowIfNull(view);
        var pieces = new List<ScenePiece>();
        var cityTiles = new HashSet<HexCoord>();
        foreach (var city in view.Cities.Where(city => view.Contains(city.Position)))
        {
            cityTiles.Add(city.Position);
            pieces.Add(Piece(view, city.Position, 0, PieceModel.City, city.Owner) with { Name = city.Name, Icon = Icons.City });
        }

        foreach (var unit in view.Units.Where(unit => view.Contains(unit.Position)))
        {
            var offset = cityTiles.Contains(unit.Position) ? BesideCity : 0;
            var health = maxHealth > 0 ? Math.Clamp((double)unit.Health / maxHealth, 0, 1) : 1;
            pieces.Add(Piece(view, unit.Position, offset, PieceModels.For(unit.Type), unit.Owner) with { Health = health, Icon = Icons.Unit(unit.Type), Unit = unit.Id.Value });
        }

        return pieces;
    }

    /// <summary>
    /// A unit the view no longer holds, drawn while it animates (walking out of sight or fading after being destroyed): its model,
    /// seat and icon at a tile, unhurt, with no unit id.
    /// </summary>
    /// <param name="view">The player's view (for the seat and the tile's surface).</param>
    /// <param name="at">The tile it stands on.</param>
    /// <param name="type">Its type.</param>
    /// <param name="owner">Its owner.</param>
    /// <returns>The piece.</returns>
    public static ScenePiece Ghost(PlayerView view, HexCoord at, UnitType type, PlayerId owner)
    {
        ArgumentNullException.ThrowIfNull(view);
        return Piece(view, at, 0, PieceModels.For(type), owner) with { Icon = Icons.Unit(type) };
    }

    // A piece at full health with no name or icon yet; a city is the City model, anything else a unit.
    private static ScenePiece Piece(PlayerView view, HexCoord coord, double offset, PieceModel model, PlayerId owner)
    {
        var (x, z) = BoardSceneBuilder.Center(coord);
        return new ScenePiece(
            coord.Q, coord.R, x + offset, z + offset, TerrainReliefs.Surface(view.TileAt(coord)), model == PieceModel.City ? "city" : "unit",
            model.ToString().ToLowerInvariant(), Seats.Number(view, owner), owner == view.Viewer, 1, null, "");
    }
}
