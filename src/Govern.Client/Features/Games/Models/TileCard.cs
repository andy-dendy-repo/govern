using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// What the board's tile card says about one tile (built by <see cref="Helpers.TileCardBuilder"/>): its terrain as the title, then
/// one short sentence per thing on or around it. The same text is read out to screen readers for the tile the player clicks.
/// </summary>
/// <param name="Tile">The tile described.</param>
/// <param name="Title">The terrain's name.</param>
/// <param name="Lines">The city, unit, building or improvement, territory, rivers, bridges and the lens's sentences, in that order.</param>
public sealed record TileCard(HexCoord Tile, string Title, IReadOnlyList<string> Lines)
{
    /// <summary>The whole card as one text: the title and every line, each ending with a full stop.</summary>
    public string Text => string.Join(" ", new[] { Title }.Concat(Lines).Select(static line => $"{line}."));
}
