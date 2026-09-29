using System.Globalization;
using Govern.Client.Features.Games.Models;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The board's pan and zoom: a point in board space is drawn at <c>point * Scale + (X, Y)</c> on screen.
/// </summary>
/// <param name="Scale">The zoom, between <see cref="MinScale"/> and <see cref="MaxScale"/>.</param>
/// <param name="X">The horizontal offset in screen pixels.</param>
/// <param name="Y">The vertical offset in screen pixels.</param>
public sealed record BoardViewport(double Scale, double X, double Y)
{
    /// <summary>The smallest zoom.</summary>
    public const double MinScale = 0.4;

    /// <summary>The largest zoom.</summary>
    public const double MaxScale = 3;

    /// <summary>The SVG <c>transform</c> of the board layer (always with <c>.</c> decimals).</summary>
    public string Transform =>
        string.Create(CultureInfo.InvariantCulture, $"translate({X:0.##} {Y:0.##}) scale({Scale:0.####})");

    /// <summary>Shows the whole of <paramref name="bounds"/>, centred, as large as the element allows.</summary>
    /// <param name="bounds">The map's rectangle in board space.</param>
    /// <param name="width">The board element's width in pixels.</param>
    /// <param name="height">The board element's height in pixels.</param>
    /// <returns>The viewport.</returns>
    public static BoardViewport Fit(BoardRect bounds, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        var scale = Clamp(Math.Min(width / bounds.Width, height / bounds.Height));
        return new BoardViewport(
            scale,
            ((width - (bounds.Width * scale)) / 2) - (bounds.Left * scale),
            ((height - (bounds.Height * scale)) / 2) - (bounds.Top * scale));
    }

    /// <summary>Moves the board by a number of screen pixels.</summary>
    /// <param name="dx">Pixels to the right.</param>
    /// <param name="dy">Pixels down.</param>
    /// <returns>The moved viewport.</returns>
    public BoardViewport Pan(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    /// <summary>Zooms by <paramref name="factor"/>, keeping the board point under the screen point (x, y) where it is.</summary>
    /// <param name="x">The anchor's x in element pixels.</param>
    /// <param name="y">The anchor's y in element pixels.</param>
    /// <param name="factor">Above 1 zooms in, below 1 zooms out; the result is clamped.</param>
    /// <returns>The zoomed viewport.</returns>
    public BoardViewport ZoomAt(double x, double y, double factor)
    {
        var scale = Clamp(Scale * factor);
        var ratio = scale / Scale;
        return new BoardViewport(scale, x - ((x - X) * ratio), y - ((y - Y) * ratio));
    }

    private static double Clamp(double scale) =>
        double.IsFinite(scale) ? Math.Clamp(scale, MinScale, MaxScale) : 1;
}
