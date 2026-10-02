namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What a map lens shows on one tile.
/// </summary>
/// <param name="Label">The short text drawn on the tile (a number or a symbol).</param>
/// <param name="Class">The value's tint: the board colours the tile with the <c>--gv-lens-&lt;class&gt;</c> token.</param>
/// <param name="Description">The same information as a sentence: the board's tile card (<c>TileCardBuilder</c>) shows it for the
/// tile under the pointer and reads it out to screen readers for the tile the player clicks.</param>
public sealed record LensMark(string Label, string Class, string Description);
