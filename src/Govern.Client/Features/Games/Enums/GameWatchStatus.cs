namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// The state of a live game connection, as shown to the player.
/// </summary>
public enum GameWatchStatus
{
    /// <summary>Opening the first connection.</summary>
    Connecting,

    /// <summary>Connected; snapshots arrive as the game changes.</summary>
    Live,

    /// <summary>Disconnected; the next attempt is due after a delay.</summary>
    Reconnecting,

    /// <summary>The game no longer exists for the player (cancelled, expired or left). Final.</summary>
    Gone,

    /// <summary>The session ended. Final.</summary>
    SignedOut,

    /// <summary>The game is over and the stream ended. Final.</summary>
    Finished,
}
