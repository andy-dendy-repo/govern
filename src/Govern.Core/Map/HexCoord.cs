using System.Text.Json.Serialization;

namespace Govern.Core.Map;

/// <summary>
/// A hex tile position in axial coordinates (pointy-top layout). The third cube coordinate <see cref="S"/> is derived,
/// and <see cref="Column"/>/<see cref="Row"/> give the odd-r offset position used for rectangular maps and rendering.
/// </summary>
/// <param name="Q">Axial column coordinate.</param>
/// <param name="R">Axial row coordinate; equals the offset row.</param>
public readonly record struct HexCoord(int Q, int R)
{
    private static readonly HexCoord[] Directions = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

    /// <summary>Third cube coordinate, <c>-Q - R</c>.</summary>
    [JsonIgnore]
    public int S => -Q - R;

    /// <summary>Column in the odd-r offset layout (odd rows are shifted half a tile to the right).</summary>
    [JsonIgnore]
    public int Column => Q + ((R - (R & 1)) / 2);

    /// <summary>Row in the odd-r offset layout.</summary>
    [JsonIgnore]
    public int Row => R;

    /// <summary>Converts an odd-r offset position to axial coordinates.</summary>
    /// <param name="column">Offset column.</param>
    /// <param name="row">Offset row.</param>
    /// <returns>The axial coordinate of that tile.</returns>
    public static HexCoord FromOffset(int column, int row) => new(column - ((row - (row & 1)) / 2), row);

    /// <summary>The six adjacent tiles, always in the same order (east, then counter-clockwise).</summary>
    /// <returns>The neighbours; some may lie outside the map.</returns>
    public IEnumerable<HexCoord> Neighbors()
    {
        foreach (var direction in Directions)
        {
            yield return new HexCoord(Q + direction.Q, R + direction.R);
        }
    }

    /// <summary>Number of steps between two tiles on an unbounded hex grid.</summary>
    /// <param name="other">The other tile.</param>
    /// <returns>The hex distance (0 for the same tile).</returns>
    public int DistanceTo(HexCoord other) =>
        (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;

    /// <summary>Every tile within <paramref name="radius"/> steps, including this one, in a fixed order.</summary>
    /// <param name="radius">Non-negative radius.</param>
    /// <returns><c>3r(r+1)+1</c> tiles; some may lie outside the map.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative.</exception>
    public IEnumerable<HexCoord> WithinRadius(int radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        return Area(this, radius);

        static IEnumerable<HexCoord> Area(HexCoord center, int radius)
        {
            for (var dq = -radius; dq <= radius; dq++)
            {
                for (var dr = Math.Max(-radius, -dq - radius); dr <= Math.Min(radius, -dq + radius); dr++)
                {
                    yield return new HexCoord(center.Q + dq, center.R + dr);
                }
            }
        }
    }
}
