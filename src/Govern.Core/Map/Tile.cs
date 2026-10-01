using Govern.Core.Cities;
using Govern.Core.Economy;
using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// One map tile.
/// </summary>
/// <param name="Coord">Position of the tile.</param>
/// <param name="Terrain">Terrain on the tile.</param>
/// <param name="Rivers">
/// Rivers on the edges this tile owns (<see cref="HexSide"/>), or <see langword="null"/> when there are none. Left out of the
/// JSON while null, so maps saved before rivers existed read unchanged.
/// </param>
/// <param name="Bridges">Bridges on the river edges this tile owns, or <see langword="null"/> when there are none.</param>
/// <param name="Improvement">The improvement built on the tile, if any (at most one).</param>
/// <param name="Territory">
/// The city whose territory the tile is in, or <see langword="null"/> when no city owns it. A tile changes hands only with its
/// city (a capture), never by itself.
/// </param>
/// <param name="Building">
/// The building that stands on the tile, if any (at most one); it belongs to the city whose <paramref name="Territory"/> holds the
/// tile and works for that city as a whole.
/// </param>
public sealed record Tile(
    HexCoord Coord,
    TerrainType Terrain,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileRivers? Rivers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileBridges? Bridges = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImprovementType? Improvement = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CityId? Territory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingType? Building = null);
