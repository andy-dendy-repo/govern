namespace Govern.Core.Maps;

/// <summary>
/// Kind of terrain on a tile; its movement rules live in <see cref="TerrainCatalog"/>.
/// </summary>
public enum TerrainType
{
    /// <summary>Open land: passable, movement cost 1.</summary>
    Plains,
}
