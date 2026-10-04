using System.Collections.Immutable;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// A hand-made map from the map pool (<c>maps/&lt;key&gt;.json</c>), played instead of a generated one when a game's
/// <see cref="State.GameSetup"/> names it. Unlike a generated map its two halves need not be mirrors: each side has a land of its
/// own, and the validator checks that they are worth the same (their yields, and what lies around every spawn candidate). A game
/// keeps its own copy of the map in its state, so editing a map file never changes a running game.
/// </summary>
/// <param name="Key">The map's id, lower snake_case, such as <c>basin</c>.</param>
/// <param name="Name">The name players see, such as <c>The Basin</c>.</param>
/// <param name="Map">The terrain and rivers.</param>
/// <param name="FirstSpawns">
/// The first player's candidate spawn tiles (at least 2), in the left half. The game's seed picks one for each player on its
/// own, so a player's own start doesn't tell them which of the opponent's candidates they got. The candidates are public, so
/// they do narrow the opponent's start down to these few tiles: an accepted leak.
/// </param>
/// <param name="SecondSpawns">The second player's candidate spawn tiles (at least 2), in the right half.</param>
public sealed record PredefinedMap(string Key, string Name, GameMap Map, ImmutableArray<HexCoord> FirstSpawns, ImmutableArray<HexCoord> SecondSpawns)
{
    /// <summary>Parses and validates a map document against the ruleset it will be played with.</summary>
    /// <param name="ruleset">The ruleset (its terrains, rivers, map size limits and starting units are checked).</param>
    /// <param name="json">The document text.</param>
    /// <returns>The map, or every problem found.</returns>
    public static PredefinedMapParseResult Parse(Ruleset ruleset, string json) => PredefinedMapParser.Parse(ruleset, json);

    /// <summary>Parses a map document that is known to be valid, such as one embedded in the map pool.</summary>
    /// <param name="ruleset">The ruleset the map is played with.</param>
    /// <param name="json">The document text.</param>
    /// <returns>The map.</returns>
    /// <exception cref="FormatException">The document is not a valid map; the message lists every problem.</exception>
    public static PredefinedMap ParseValid(Ruleset ruleset, string json)
    {
        var result = Parse(ruleset, json);
        return result.IsValid
            ? result.Map
            : throw new FormatException($"The map is invalid: {string.Join("; ", result.Problems)}");
    }
}
