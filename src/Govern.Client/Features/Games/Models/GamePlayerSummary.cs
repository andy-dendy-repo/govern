namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A seat in a game, as the caller sees it.
/// </summary>
/// <param name="PlayerId">The per-game player id.</param>
/// <param name="Seat">0 for the creator, 1 for the joiner.</param>
/// <param name="IsYou">Whether this seat is the caller's.</param>
/// <param name="IsCreator">Whether this player created the game.</param>
public sealed record GamePlayerSummary(Guid PlayerId, int Seat, bool IsYou, bool IsCreator);
