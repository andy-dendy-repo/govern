using Govern.Domain.Enums;

namespace Govern.Application.Games.Models;

/// <summary>
/// One row of the caller's game list.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="Phase">Lobby, in progress or finished.</param>
/// <param name="PlayerCount">Seats taken.</param>
/// <param name="Seats">Seats in the game (2 to 4).</param>
/// <param name="IsCreator">Whether the caller created the game.</param>
/// <param name="IsYourTurn">
/// Whether the game is running and waiting for the caller's move: their turn in a sequential game, or in a simultaneous game a
/// round they still play and haven't ended.
/// </param>
/// <param name="IsOut">Whether the caller resigned or was eliminated while the game goes on for the others.</param>
/// <param name="ActivityAt">
/// When the game last changed in a way every player sees (a seat taken or left, the start, a turn change, the finish); the
/// list is ordered by it, newest first. Moves hidden from the caller never change it.
/// </param>
public sealed record MyGameItem(
    Guid Id, GamePhase Phase, int PlayerCount, int Seats, bool IsCreator, bool IsYourTurn, bool IsOut, DateTimeOffset ActivityAt);
