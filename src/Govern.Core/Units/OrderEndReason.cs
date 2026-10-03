namespace Govern.Core.Units;

/// <summary>
/// Why a unit stopped walking to its destination on its own (see <see cref="Unit.Destination"/>).
/// </summary>
public enum OrderEndReason
{
    /// <summary>The unit reached its destination.</summary>
    Arrived,

    /// <summary>No path over the tiles its owner knows leads there any more.</summary>
    Blocked,

    /// <summary>An enemy unit or city came into its owner's sight while it walked, so the owner decides what to do.</summary>
    EnemySighted,
}
