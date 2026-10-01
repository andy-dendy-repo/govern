namespace Govern.Core.Map;

/// <summary>
/// The three edges a tile owns (<see cref="TileRivers"/>): every edge between two tiles belongs to exactly one of them, so it
/// is stored once. A tile's other three edges (west, south-west, south-east) belong to its neighbours on those sides.
/// </summary>
public enum HexSide
{
    /// <summary>The edge towards the east neighbour (axial +1, 0).</summary>
    East,

    /// <summary>The edge towards the north-east neighbour (axial +1, −1).</summary>
    NorthEast,

    /// <summary>The edge towards the north-west neighbour (axial 0, −1).</summary>
    NorthWest,
}
