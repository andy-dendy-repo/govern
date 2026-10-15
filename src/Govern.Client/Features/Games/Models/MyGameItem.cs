using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// One row of the caller's game list.
/// </summary>
/// <param name="Id">The game's id.</param>
/// <param name="Phase">Lobby, in progress or finished.</param>
/// <param name="PlayerCount">Seats taken.</param>
/// <param name="Seats">Seats in the game (2 to 4).</param>
/// <param name="IsCreator">Whether the caller created the game.</param>
/// <param name="IsYourTurn">Whether the game is in progress and waiting for the caller.</param>
/// <param name="IsOut">Whether the caller resigned or was eliminated while the game goes on for the others.</param>
/// <param name="ActivityAt">The last change every player can see; the list is sorted by it, newest first.</param>
public sealed record MyGameItem(
    Guid Id, GamePhase Phase, int PlayerCount, int Seats, bool IsCreator, bool IsYourTurn, bool IsOut, DateTimeOffset ActivityAt);
