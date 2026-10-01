using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// One line of the game log as shown: a sentence and the tile it happened on.
/// </summary>
/// <param name="Text">The sentence, such as "Your Archer hit Seat 2's Soldier: 22 damage".</param>
/// <param name="Tile">The tile to show when the line is clicked; <see langword="null"/> for events with no place (a turn).</param>
/// <param name="IsYours">Whether the line is about the player's own action or units (styled differently from the opponent's).</param>
public sealed record GameLogLine(string Text, HexCoord? Tile, bool IsYours);
