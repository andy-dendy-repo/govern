using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Map;
using Govern.Core.Rulesets;
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
/// <param name="Units">
/// The viewer's units and the enemy units on visible tiles, ordered by id; enemy movement is hidden (<see cref="UnitView"/>).
/// </param>
/// <param name="Cities">
/// The viewer's cities and the enemy cities on visible tiles, ordered by id; enemy production is hidden (<see cref="CityView"/>).
/// </param>
/// <param name="Players">Participants in turn order.</param>
/// <param name="TradeDeals">
/// The trade deals the viewer is a side of (<see cref="TradeDealView"/>), ordered by id; <see langword="null"/> when none.
/// </param>
/// <param name="Science">The viewer's own science per turn and science nodes (<see cref="ScienceView"/>).</param>
/// <param name="TradePartners">
/// Other players' cities the viewer trades with (<see cref="TradePartnerView"/>): those whose trade post they have seen, and the
/// other side of their deals; ordered by id, <see langword="null"/> when none.
/// </param>
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
    ImmutableArray<UnitView> Units,
    ImmutableArray<CityView> Cities,
    ImmutableArray<PlayerSummary> Players,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<TradeDealView>? TradeDeals = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScienceView? Science = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<TradePartnerView>? TradePartners = null)
{
    /// <summary>The trade deals the viewer is a side of (empty when none).</summary>
    [JsonIgnore]
    public ImmutableArray<TradeDealView> TradeDealList => TradeDeals ?? [];

    /// <summary>Other players' cities the viewer trades with (empty when none).</summary>
    [JsonIgnore]
    public ImmutableArray<TradePartnerView> TradePartnerList => TradePartners ?? [];

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

    /// <summary>The river the viewer knows of on the edge between two neighbouring tiles.</summary>
    /// <param name="a">One tile.</param>
    /// <param name="b">The other tile.</param>
    /// <returns>
    /// The river, or <see langword="null"/> when there is none, the tiles are not neighbours, or the tile that owns the edge is
    /// off the map or unexplored.
    /// </returns>
    public RiverType? RiverBetween(HexCoord a, HexCoord b) =>
        HexEdge.Between(a, b) is { } edge && Contains(edge.Owner) ? TileAt(edge.Owner).Rivers?.On(edge.Side) : null;

    /// <summary>Whether the viewer sees a bridge on the edge between two neighbouring tiles.</summary>
    /// <param name="a">One tile.</param>
    /// <param name="b">The other tile.</param>
    /// <returns><see langword="true"/> when the edge is bridged and the tile that owns it is visible.</returns>
    public bool BridgeBetween(HexCoord a, HexCoord b) =>
        HexEdge.Between(a, b) is { } edge && Contains(edge.Owner) && TileAt(edge.Owner).Bridges?.On(edge.Side) == true;

    /// <summary>
    /// The river the viewer knows a unit crosses from one tile to its neighbour: the river between them unless a bridge spans it
    /// (see <see cref="GameMap.CrossedRiverBetween"/>).
    /// </summary>
    /// <param name="a">The tile the unit is on.</param>
    /// <param name="b">The neighbouring tile.</param>
    /// <returns>The river crossed, or <see langword="null"/>.</returns>
    public RiverType? CrossedRiverBetween(HexCoord a, HexCoord b) => BridgeBetween(a, b) ? null : RiverBetween(a, b);

    /// <summary>The rivers on the six edges of a tile, bridged or not, each edge at most once.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The river of every edge that has one.</returns>
    public IEnumerable<RiverType> RiversAround(HexCoord coord) =>
        coord.Neighbors().Select(neighbor => RiverBetween(coord, neighbor)).OfType<RiverType>();

    /// <summary>
    /// Returns the unit holding a tile, if the viewer sees one there: the one unit on it that doesn't share tiles (traders do,
    /// see <see cref="Units.UnitDefinition.SharesTiles"/>).
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="coord">The tile.</param>
    /// <returns>The unit, or <see langword="null"/>.</returns>
    public UnitView? HolderAt(Ruleset ruleset, HexCoord coord)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return Units.FirstOrDefault(unit => unit.Position == coord && !ruleset.Unit(unit.Type).SharesTiles);
    }

    /// <summary>
    /// Returns the unit an attack by the viewer on a tile would hit, as the rules pick it: the unit holding the tile, or with
    /// none another player's trader there, unless the tile is a city (an undefended city is captured).
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="coord">The tile.</param>
    /// <returns>The unit, or <see langword="null"/> when an attack would hit no unit.</returns>
    public UnitView? DefenderAt(Ruleset ruleset, HexCoord coord) =>
        Actions.AttackRule.DefenderAt(ruleset, UnitsAt(coord), CityAt(coord) is not null, Viewer);

    /// <summary>Returns every unit the viewer sees on a tile: its holder, if any, and the traders sharing it.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The units, ordered by id (empty when none).</returns>
    public IEnumerable<UnitView> UnitsAt(HexCoord coord) => Units.Where(unit => unit.Position == coord);

    /// <summary>Returns the city on a tile, if the viewer sees one there.</summary>
    /// <param name="coord">The tile.</param>
    /// <returns>The city, or <see langword="null"/>.</returns>
    public CityView? CityAt(HexCoord coord) => Cities.FirstOrDefault(city => city.Position == coord);

    /// <summary>Returns one of the viewer's own units.</summary>
    /// <param name="id">The unit.</param>
    /// <returns>The unit, or <see langword="null"/> when the viewer has no unit with this id.</returns>
    public UnitView? FindOwnUnit(UnitId id) => Units.FirstOrDefault(unit => unit.Id == id && unit.Owner == Viewer);
}
