using System.Globalization;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Places pointy-top hexes on the board in pixels (board space, before the viewport's pan and zoom). Coordinates are
/// axial, as in Core; the map is an odd-r offset rectangle.
/// </summary>
public static class HexLayout
{
    /// <summary>The distance from a hex's centre to a corner, in pixels.</summary>
    public const double Size = 30;

    /// <summary>The distance between the centres of two hexes next to each other in a row.</summary>
    public static readonly double Width = Math.Sqrt(3) * Size;

    /// <summary>
    /// The corners of a hex centred on the origin, as an SVG <c>points</c> list (always with <c>.</c> decimals).
    /// </summary>
    public static readonly string Corners = string.Join(' ', Enumerable.Range(0, 6).Select(static corner =>
    {
        var (x, y) = Corner(corner);
        return string.Create(CultureInfo.InvariantCulture, $"{x:0.###},{y:0.###}");
    }));

    /// <summary>The centre of a hex.</summary>
    /// <param name="coord">The hex.</param>
    /// <returns>The centre's x and y.</returns>
    public static (double X, double Y) Center(HexCoord coord) => (Width * (coord.Q + (coord.R / 2.0)), Size * 1.5 * coord.R);

    /// <summary>The two corners at the ends of a tile edge (where a river runs).</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>Both corners' x and y.</returns>
    public static (double X1, double Y1, double X2, double Y2) EdgeEnds(HexEdge edge)
    {
        // Corner k sits at angle 60k − 30 degrees (y grows downwards): 0 upper right, 1 lower right, 4 upper left, 5 top.
        var (first, second) = edge.Side switch
        {
            HexSide.East => (0, 1),
            HexSide.NorthEast => (5, 0),
            HexSide.NorthWest => (4, 5),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge.Side, "Unknown side."),
        };
        var (x, y) = Center(edge.Owner);
        var (x1, y1) = Corner(first);
        var (x2, y2) = Corner(second);
        return (x + x1, y + y1, x + x2, y + y2);
    }

    /// <summary>The middle of a tile edge.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The midpoint's x and y.</returns>
    public static (double X, double Y) EdgeMiddle(HexEdge edge)
    {
        var (x1, y1, x2, y2) = EdgeEnds(edge);
        return ((x1 + x2) / 2, (y1 + y2) / 2);
    }

    /// <summary>
    /// The ends of a bridge over a tile edge: a short line across the edge's middle, from one tile towards the other, as long
    /// as half the distance between their centres.
    /// </summary>
    /// <param name="edge">The bridged edge.</param>
    /// <returns>Both ends' x and y.</returns>
    public static (double X1, double Y1, double X2, double Y2) BridgeEnds(HexEdge edge)
    {
        var (ax, ay) = Center(edge.Owner);
        var (bx, by) = Center(edge.Neighbor());
        var (mx, my) = ((ax + bx) / 2, (ay + by) / 2);
        var (dx, dy) = ((bx - ax) / 4, (by - ay) / 4);
        return (mx - dx, my - dy, mx + dx, my + dy);
    }

    /// <summary>The smallest rectangle holding every hex of a map.</summary>
    /// <param name="width">Map columns; at least 1.</param>
    /// <param name="height">Map rows; at least 1.</param>
    /// <returns>The rectangle.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is below 1.</exception>
    public static BoardRect Bounds(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        // Odd rows are shifted half a hex to the right.
        var columns = height > 1 ? width + 0.5 : width;
        return new BoardRect(-Width / 2, -Size, Width * columns, (Size * 1.5 * (height - 1)) + (2 * Size));
    }

    // A hex corner relative to the centre.
    private static (double X, double Y) Corner(int corner)
    {
        var angle = Math.PI / 180 * ((60 * corner) - 30);
        return (Size * Math.Cos(angle), Size * Math.Sin(angle));
    }

    /// <summary>Formats a number for an SVG attribute (always with <c>.</c> decimals).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
