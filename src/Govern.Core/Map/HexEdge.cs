namespace Govern.Core.Map;

/// <summary>
/// The edge between two neighbouring tiles, named by the tile that owns it and the side (<see cref="HexSide"/>).
/// </summary>
/// <param name="Owner">The tile that stores the edge.</param>
/// <param name="Side">Which of the owner's three sides it is.</param>
public readonly record struct HexEdge(HexCoord Owner, HexSide Side)
{
    /// <summary>The edge between two tiles.</summary>
    /// <param name="a">One tile.</param>
    /// <param name="b">The other tile.</param>
    /// <returns>The edge, or <see langword="null"/> when the tiles are not neighbours.</returns>
    public static HexEdge? Between(HexCoord a, HexCoord b) => (b.Q - a.Q, b.R - a.R) switch
    {
        (1, 0) => new HexEdge(a, HexSide.East),
        (1, -1) => new HexEdge(a, HexSide.NorthEast),
        (0, -1) => new HexEdge(a, HexSide.NorthWest),
        (-1, 0) => new HexEdge(b, HexSide.East),
        (-1, 1) => new HexEdge(b, HexSide.NorthEast),
        (0, 1) => new HexEdge(b, HexSide.NorthWest),
        _ => null,
    };

    /// <summary>The tile on the other side of the edge from <see cref="Owner"/>.</summary>
    /// <returns>The owner's neighbour across this edge.</returns>
    public HexCoord Neighbor() => Side switch
    {
        HexSide.East => new HexCoord(Owner.Q + 1, Owner.R),
        HexSide.NorthEast => new HexCoord(Owner.Q + 1, Owner.R - 1),
        HexSide.NorthWest => new HexCoord(Owner.Q, Owner.R - 1),
        _ => throw new InvalidOperationException($"Unknown side {Side}."),
    };
}
