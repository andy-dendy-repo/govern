namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What a map lens shows on one tile.
/// </summary>
/// <param name="Label">The short text drawn on the tile (a number or a symbol).</param>
/// <param name="Class">The CSS class that tints the tile for this value (in <c>board.css</c>, <c>.lens-mark.&lt;class&gt;</c>).</param>
/// <param name="Description">The same information as a sentence, drawn as the mark's SVG <c>title</c> for assistive technology.</param>
public sealed record LensMark(string Label, string Class, string Description);
