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
public sealed record Tile(
    HexCoord Coord,
    TerrainType Terrain,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileRivers? Rivers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TileBridges? Bridges = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImprovementType? Improvement = null);
