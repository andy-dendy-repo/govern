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
        var angle = Math.PI / 180 * ((60 * corner) - 30);
        return string.Create(CultureInfo.InvariantCulture, $"{Size * Math.Cos(angle):0.###},{Size * Math.Sin(angle):0.###}");
    }));

    /// <summary>The centre of a hex.</summary>
    /// <param name="coord">The hex.</param>
    /// <returns>The centre's x and y.</returns>
    public static (double X, double Y) Center(HexCoord coord) => (Width * (coord.Q + (coord.R / 2.0)), Size * 1.5 * coord.R);

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

    /// <summary>Formats a number for an SVG attribute (always with <c>.</c> decimals).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
