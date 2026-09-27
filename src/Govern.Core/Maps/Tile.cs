using Govern.Core.Hexes;

namespace Govern.Core.Maps;

/// <summary>
/// One map tile.
/// </summary>
/// <param name="Coord">Position of the tile.</param>
/// <param name="Terrain">Terrain on the tile.</param>
public sealed record Tile(HexCoord Coord, TerrainType Terrain);
