namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// Where a game is in its lifecycle; sent by the Api as a camelCase string.
/// </summary>
public enum GamePhase
{
    /// <summary>Waiting for the second player; the creator may start once both seats are taken.</summary>
    Lobby,

    /// <summary>Started: the players take turns.</summary>
    InProgress,

    /// <summary>Over.</summary>
    Finished,
}
