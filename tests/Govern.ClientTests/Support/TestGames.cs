using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;

namespace Govern.ClientTests.Support;

/// <summary>Game responses shaped like the Api's, from the caller's point of view.</summary>
public static class TestGames
{
    public const string InviteCode = "AbCdEfGhIjKlMnOpQrSt_-";

    public static readonly Guid Id = Guid.Parse("0199a1b2-0000-7000-8000-000000000001");

    public static readonly DateTimeOffset CreatedAt = new(2026, 9, 28, 11, 0, 0, TimeSpan.Zero);

    /// <summary>A lobby as its creator sees it, with 1 or 2 seats taken.</summary>
    public static GetGameResponse CreatorLobby(int players = 1, Guid? id = null) =>
        new(id ?? Id, GamePhase.Lobby, IsCreator: true, InviteCode, Seats(players, callerSeat: 0), CreatedAt);

    /// <summary>A lobby as the joiner sees it (no invite code).</summary>
    public static GetGameResponse JoinerLobby() =>
        new(Id, GamePhase.Lobby, IsCreator: false, null, Seats(2, callerSeat: 1), CreatedAt);

    /// <summary>A started or finished game as its creator sees it.</summary>
    public static GetGameResponse Started(GamePhase phase = GamePhase.InProgress) =>
        new(Id, phase, IsCreator: true, null, Seats(2, callerSeat: 0), CreatedAt);

    public static string StreamPath(Guid? id = null) => $"/api/games/{id ?? Id}/stream";

    public static string GamePath(Guid? id = null) => $"/api/games/{id ?? Id}";

    private static List<GamePlayerSummary> Seats(int players, int callerSeat) =>
        [.. Enumerable.Range(0, players).Select(seat => new GamePlayerSummary(Guid.CreateVersion7(), seat, seat == callerSeat, seat == 0))];
}
