namespace Govern.Core.State;

/// <summary>
/// How the players of a game take their turns (F9), chosen in the lobby and stored in the <see cref="GameState"/>.
/// </summary>
public enum TurnMode
{
    /// <summary>One player at a time, in seat order (<see cref="GameState.CurrentPlayer"/>). The default.</summary>
    Sequential,

    /// <summary>
    /// Every active player acts at once and ends their turn (<see cref="Player.EndedTurn"/>); the round resolves for everyone
    /// when the last active player has ended.
    /// </summary>
    Simultaneous,
}
