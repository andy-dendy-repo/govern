namespace Govern.Core.Visibility;

/// <summary>
/// What a player knows about a tile.
/// </summary>
public enum TileVisibility
{
    /// <summary>Never seen: terrain unknown, contents hidden.</summary>
    Unexplored,

    /// <summary>Seen before but not now: terrain known, units and cities hidden.</summary>
    Explored,

    /// <summary>In sight of one of the player's units or cities now: everything on it is shown.</summary>
    Visible,
}
