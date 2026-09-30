namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The game the caller joined (or already played in), from <c>POST /api/games/join</c>.
/// </summary>
/// <param name="GameId">The game's id.</param>
public sealed record JoinGameResponse(Guid GameId);
