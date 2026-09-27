namespace Govern.Core.State;

/// <summary>
/// Lifecycle of a running game.
/// </summary>
public enum GameStatus
{
    /// <summary>Players take turns.</summary>
    InProgress,

    /// <summary>A winner is known (or nobody is left); every command is rejected.</summary>
    Finished,
}
