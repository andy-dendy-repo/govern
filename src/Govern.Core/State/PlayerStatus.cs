namespace Govern.Core.State;

/// <summary>
/// Whether a player still takes part in the game.
/// </summary>
public enum PlayerStatus
{
    /// <summary>Still playing.</summary>
    Active,

    /// <summary>Lost every city and settler.</summary>
    Eliminated,

    /// <summary>Gave up.</summary>
    Resigned,
}
