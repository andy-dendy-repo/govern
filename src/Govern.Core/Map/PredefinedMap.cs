using System.Collections.Immutable;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

/// <summary>
/// A hand-made map from the map pool (<c>maps/&lt;key&gt;.json</c>), played instead of a generated one when a game's
/// <see cref="State.GameSetup"/> names it. It is made for as many players as it has spawn groups. Unlike a generated map its
/// sides need not be mirrors: each player has a land of their own, and the validator checks that they are worth the same (what
/// lies around every spawn candidate and, on a two-player map, the yields of each half). A game keeps its own copy of the map
/// in its state, so editing a map file never changes a running game.
/// </summary>
/// <param name="Key">The map's id, lower snake_case, such as <c>basin</c>.</param>
/// <param name="Name">The name players see, such as <c>The Basin</c>.</param>
/// <param name="Map">The terrain and rivers.</param>
/// <param name="Spawns">
/// One group of candidate spawn tiles (at least 2 each) per seat, in seat order. The game's seed picks one tile of each group on
/// its own, so a player's own start doesn't tell them which of the others' candidates they got. The candidates are public, so
/// they do narrow every other start down to these few tiles: an accepted leak.
/// </param>
public sealed record PredefinedMap(string Key, string Name, GameMap Map, ImmutableArray<ImmutableArray<HexCoord>> Spawns)
{
    /// <summary>The number of players the map is made for: one per spawn group.</summary>
    public int Players => Spawns.Length;

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
