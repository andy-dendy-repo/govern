using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What a map lens shows on one edge of a tile, such as the extra cost of crossing a river.
/// </summary>
/// <param name="Side">Which of the tile's own edges (<see cref="HexSide"/>) the mark sits on.</param>
/// <param name="Label">The short text drawn on the edge (a number or a symbol).</param>
/// <param name="Class">The value's tint, as for <see cref="LensMark.Class"/> (a <c>blocked</c> badge is drawn in the danger colour).</param>
/// <param name="Description">The same information as a sentence: the board's tile card (<c>TileCardBuilder</c>) shows it for the
/// tile under the pointer and reads it out to screen readers for the tile the player clicks.</param>
public sealed record EdgeMark(HexSide Side, string Label, string Class, string Description);
