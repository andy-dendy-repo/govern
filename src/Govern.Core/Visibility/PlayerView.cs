using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// The game as one player may see it: the only shape sent to a client. It hides unexplored terrain and every enemy unit or city
/// that is not on a currently visible tile, so reading it in the browser reveals nothing the player could not see on screen.
/// </summary>
/// <param name="Viewer">Player this view is for.</param>
/// <param name="Version">
/// Opaque fingerprint of this view (a hash of everything else in it): equal views have equal versions. Compare it only for
/// equality (the client echoes it as <c>expectedVersion</c>); it is not ordered and does not count commands, so it reveals
/// nothing the viewer cannot already see.
/// </param>
/// <param name="Turn">Round number.</param>
/// <param name="CurrentPlayer">Player who may act now.</param>
/// <param name="Status">Whether the game is still running.</param>
/// <param name="Winner">The winner once finished; otherwise <see langword="null"/>.</param>
/// <param name="Width">Map columns.</param>
/// <param name="Height">Map rows.</param>
/// <param name="Tiles">
/// Every tile, row-major (index <c>row * Width + column</c>), with what the viewer knows about it; <see cref="CoordAt"/> gives a
/// tile's position and <see cref="TileAt"/> the tile at a position.
/// </param>
/// <param name="Units">The viewer's units and the enemy units on visible tiles, ordered by id.</param>
/// <param name="Cities">
/// The viewer's cities and the enemy cities on visible tiles, ordered by id; enemy production is hidden (<see cref="CityView"/>).
/// </param>
/// <param name="Players">Participants in turn order.</param>
public sealed record PlayerView(
    PlayerId Viewer,
    long Version,
    int Turn,
    PlayerId CurrentPlayer,
    GameStatus Status,
    PlayerId? Winner,
    int Width,
    int Height,
    ImmutableArray<TileView> Tiles,
    ImmutableArray<Unit> Units,
    ImmutableArray<CityView> Cities,
    ImmutableArray<PlayerSummary> Players)
{
    /// <summary>The position of the tile at an index of <see cref="Tiles"/>.</summary>
    /// <param name="index">A tile index, 0 to <c>Width * Height - 1</c>.</param>
    /// <returns>The tile's coordinate.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the map.</exception>
    public HexCoord CoordAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Width * Height);
        return HexCoord.FromOffset(index % Width, index / Width);
    }

    /// <summary>Whether the coordinate lies on the map.</summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <returns><see langword="true"/> when the tile exists.</returns>
    public bool Contains(HexCoord coord) =>
        coord.Row >= 0 && coord.Row < Height && coord.Column >= 0 && coord.Column < Width;

    /// <summary>Returns what the viewer knows about the tile at a coordinate.</summary>
    /// <param name="coord">A coordinate on the map.</param>
    /// <returns>The tile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate is outside the map.</exception>
    public TileView TileAt(HexCoord coord)
    {
        if (!Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The coordinate is outside the map.");
        }

        return Tiles[(coord.Row * Width) + coord.Column];
    }

    /// <summary>Returns the unit on a tile, if the viewer sees one there.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The unit, or <see langword="null"/>.</returns>
    public Unit? UnitAt(HexCoord coord) => Units.FirstOrDefault(unit => unit.Position == coord);

    /// <summary>Returns the city on a tile, if the viewer sees one there.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The city, or <see langword="null"/>.</returns>
    public CityView? CityAt(HexCoord coord) => Cities.FirstOrDefault(city => city.Position == coord);

    /// <summary>Returns one of the viewer's own units.</summary>
    /// <param name="id">The unit.</param>
    /// <returns>The unit, or <see langword="null"/> when the viewer has no unit with this id.</returns>
    public Unit? FindOwnUnit(UnitId id) => Units.FirstOrDefault(unit => unit.Id == id && unit.Owner == Viewer);
}
