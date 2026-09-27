namespace Govern.Domain.Enums;

/// <summary>
/// Where a <see cref="Entities.Game"/> is in its lifecycle. Stored as a string, so values may be added but never renamed.
/// </summary>
public enum GamePhase
{
    /// <summary>Waiting for players; the invite code is valid and the creator may start once the game is full.</summary>
    Lobby,

    /// <summary>Started: the game has a state and players take turns.</summary>
    InProgress,

    /// <summary>Over: a player won or everyone else resigned.</summary>
    Finished,
}
