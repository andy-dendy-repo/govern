using Govern.Core.Map;

namespace Govern.Core.Visibility;

/// <summary>
/// One tile as a player sees it. Its position is not sent: it follows from the tile's index in <see cref="PlayerView.Tiles"/>
/// (<see cref="PlayerView.CoordAt"/>).
/// </summary>
/// <param name="Visibility">What the player knows about the tile.</param>
/// <param name="Terrain">Terrain, or <see langword="null"/> while the tile is <see cref="TileVisibility.Unexplored"/>.</param>
public sealed record TileView(TileVisibility Visibility, TerrainType? Terrain);
