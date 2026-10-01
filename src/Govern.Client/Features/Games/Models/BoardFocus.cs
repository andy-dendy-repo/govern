using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A request to centre the board on a tile (a click on a game log line). Each request has a new number, so asking for the
/// same tile again centres it again after the player panned away.
/// </summary>
/// <param name="Tile">The tile to show in the middle of the board.</param>
/// <param name="Request">Increases with every request.</param>
public sealed record BoardFocus(HexCoord Tile, int Request);
