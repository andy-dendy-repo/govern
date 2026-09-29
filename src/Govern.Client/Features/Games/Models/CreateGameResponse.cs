namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A new lobby, from <c>POST /api/games</c>.
/// </summary>
/// <param name="Id">The game's id.</param>
/// <param name="InviteCode">The code the creator shares with the second player.</param>
public sealed record CreateGameResponse(Guid Id, string InviteCode);
