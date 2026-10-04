namespace Govern.Core.Science;

/// <summary>What happened to a player's science node.</summary>
public enum ScienceNodeChange
{
    /// <summary>The player unlocked it.</summary>
    Unlocked,

    /// <summary>The player's science no longer covered it, so it was switched off (newest first).</summary>
    Disabled,

    /// <summary>The player's science covers it again, so it was switched back on (oldest first).</summary>
    Enabled,
}
