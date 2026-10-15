using System.Collections.Immutable;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

/// <summary>
/// How a game starts (the ruleset's <c>setup</c> section).
/// </summary>
/// <param name="MinPlayers">The fewest seats a game may have (at least 2).</param>
/// <param name="MaxPlayers">The most seats a game may have (at most 4, one per seat colour).</param>
/// <param name="StartingUnits">
/// The units each player starts with, in placement order: the first stands on the player's spawn tile, the k-th k columns
/// towards the map centre on the same row.
/// </param>
public sealed record SetupRules(int MinPlayers, int MaxPlayers, ImmutableArray<UnitType> StartingUnits)
{
    /// <summary>Whether a game may have this many players.</summary>
    /// <param name="players">The number of seats.</param>
    /// <returns><see langword="true"/> from <see cref="MinPlayers"/> to <see cref="MaxPlayers"/>.</returns>
    public bool Allows(int players) => players >= MinPlayers && players <= MaxPlayers;
}
