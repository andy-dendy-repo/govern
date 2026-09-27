using Govern.Domain.Enums;

namespace Govern.Application.Games.Models;

/// <summary>
/// One row of the caller's game list.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="Phase">Lobby, in progress or finished.</param>
/// <param name="PlayerCount">Seats taken.</param>
/// <param name="IsCreator">Whether the caller created the game.</param>
/// <param name="IsYourTurn">Whether the game is running and waiting for the caller's move.</param>
/// <param name="UpdatedAt">When the game last changed; the list is ordered by it, newest first.</param>
public sealed record MyGameItem(Guid Id, GamePhase Phase, int PlayerCount, bool IsCreator, bool IsYourTurn, DateTimeOffset UpdatedAt);
