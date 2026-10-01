using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What a map lens shows on one edge of a tile, such as the extra cost of crossing a river.
/// </summary>
/// <param name="Side">Which of the tile's own edges (<see cref="HexSide"/>) the mark sits on.</param>
/// <param name="Label">The short text drawn on the edge (a number or a symbol).</param>
/// <param name="Class">The CSS class that tints the mark for this value (as for <see cref="LensMark.Class"/>).</param>
/// <param name="Description">The same information as a sentence, drawn as the mark's SVG <c>title</c> for assistive technology.</param>
public sealed record EdgeMark(HexSide Side, string Label, string Class, string Description);
