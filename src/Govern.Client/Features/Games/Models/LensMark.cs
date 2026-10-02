namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What a map lens shows on one tile.
/// </summary>
/// <param name="Label">The short text drawn on the tile (a number or a symbol).</param>
/// <param name="Class">The value's tint: the board colours the tile with the <c>--gv-lens-&lt;class&gt;</c> token.</param>
/// <param name="Description">The same information as a sentence, for assistive technology. The SVG board read it out as the mark's
/// <c>title</c>; the WebGL board has no accessible text per tile yet, so nothing reads it today (a follow-up from F17).</param>
public sealed record LensMark(string Label, string Class, string Description);
