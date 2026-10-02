using System.Globalization;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Where a card kept beside a tile (the attack forecast) goes: the tile's point on screen and the side with more room. The 2D
/// board writes it as the anchor's style; the 3D board's <c>labels.js</c> writes the same properties. The CSS
/// (<c>.board-anchor</c> in <c>game-ui.css</c>) keeps the card on the board.
/// </summary>
public static class BoardAnchor
{
    /// <summary>The share of the board's width from which the card goes to the point's left instead of its right.</summary>
    public const double FlipAt = 0.55;

    /// <summary>The anchor's style: <c>--anchor-x</c>, <c>--anchor-y</c> and <c>--anchor-flip</c> (1 puts the card on the left).</summary>
    /// <param name="x">The point's x in the board element's pixels.</param>
    /// <param name="y">The point's y in the board element's pixels.</param>
    /// <param name="width">The board element's width in pixels.</param>
    /// <returns>The style, always with <c>.</c> decimals.</returns>
    public static string Style(double x, double y, double width) =>
        string.Create(CultureInfo.InvariantCulture, $"--anchor-x: {x:0.#}px; --anchor-y: {y:0.#}px; --anchor-flip: {(x > width * FlipAt ? 1 : 0)}");
}
