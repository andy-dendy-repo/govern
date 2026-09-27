using Govern.Core.Map;

namespace Govern.Core.Visibility;

/// <summary>
/// One tile as a player sees it.
/// </summary>
/// <param name="Coord">Position of the tile.</param>
/// <param name="Visibility">What the player knows about the tile.</param>
/// <param name="Terrain">Terrain, or <see langword="null"/> while the tile is <see cref="TileVisibility.Unexplored"/>.</param>
public sealed record TileView(HexCoord Coord, TileVisibility Visibility, TerrainType? Terrain);
