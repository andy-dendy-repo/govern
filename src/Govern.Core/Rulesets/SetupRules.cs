using System.Collections.Immutable;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

/// <summary>
/// How a game starts (the ruleset's <c>setup</c> section).
/// </summary>
/// <param name="Players">Exactly this many seats in a game (2 for now).</param>
/// <param name="StartingUnits">
/// The units each player starts with, in placement order: the first stands on the player's spawn tile, the k-th k columns
/// towards the map centre on the same row.
/// </param>
public sealed record SetupRules(int Players, ImmutableArray<UnitType> StartingUnits);
