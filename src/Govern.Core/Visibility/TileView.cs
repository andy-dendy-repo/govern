using Govern.Core.Economy;
using System.Text.Json.Serialization;
using Govern.Core.Map;

namespace Govern.Core.Visibility;

/// <summary>
/// One tile as a player sees it. Its position is not sent: it follows from the tile's index in <see cref="PlayerView.Tiles"/>
/// (<see cref="PlayerView.CoordAt"/>).
/// </summary>
/// <param name="Visibility">What the player knows about the tile.</param>
/// <param name="Terrain">Terrain, or <see langword="null"/> while the tile is <see cref="TileVisibility.Unexplored"/>.</param>
/// <param name="Rivers">
/// Rivers on the edges the tile owns (<see cref="HexSide"/>), or <see langword="null"/> when there are none or the tile is
/// unexplored. Rivers never change, so an explored tile shows them as they are.
/// </param>
/// <param name="Bridges">
/// Bridges on the river edges the tile owns; only on a <see cref="TileVisibility.Visible"/> tile, since bridges are built during
/// the game. <see langword="null"/> otherwise or when there are none.
/// </param>
/// <param name="Improvement">The tile's improvement; only on a <see cref="TileVisibility.Visible"/> tile.</param>
public sealed record TileView(
    TileVisibility Visibility,
    TerrainType? Terrain,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileRivers? Rivers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileBridges? Bridges = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImprovementType? Improvement = null);
