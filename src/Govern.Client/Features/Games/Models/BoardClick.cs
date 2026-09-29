using Govern.Core.Engine;

namespace Govern.Client.Features.Games.Models;

/// <summary>What a click on a tile does.</summary>
/// <param name="Next">The selection after the click.</param>
/// <param name="Command">The command to send (a move or an attack), or <see langword="null"/> when the click only selects.</param>
public sealed record BoardClick(BoardSelection Next, GameCommand? Command);
