using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A game as the caller sees it, from <c>GET /api/games/{id}</c>, <c>POST …/start</c> and each <c>game</c> stream event. The
/// Api also sends the caller's board (<c>view</c>); the client ignores it until the board is built.
/// </summary>
/// <param name="Id">The game's id.</param>
/// <param name="Phase">Lobby, in progress or finished.</param>
/// <param name="IsCreator">Whether the caller created the game (and so may start or cancel it).</param>
/// <param name="InviteCode">The invite code, only for the creator and only in the lobby; <see langword="null"/> otherwise.</param>
/// <param name="Players">Seats in order.</param>
/// <param name="CreatedAt">When the game was created.</param>
public sealed record GetGameResponse(
    Guid Id,
    GamePhase Phase,
    bool IsCreator,
    string? InviteCode,
    IReadOnlyList<GamePlayerSummary> Players,
    DateTimeOffset CreatedAt);
