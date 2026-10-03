using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.State;

/// <summary>
/// Everything needed to start a game with <see cref="GameFactory.Create"/>. The host validates user input before building it;
/// an invalid setup is a programming error and throws.
/// </summary>
/// <param name="Players">Distinct players, in turn order; as many as the ruleset's <see cref="Rulesets.SetupRules.Players"/>.</param>
/// <param name="Seed">Seed for spawn positions (and any later randomness).</param>
/// <param name="Width">Map columns within the ruleset's map size limits; <see langword="null"/> for the ruleset's default.</param>
/// <param name="Height">Map rows within the ruleset's map size limits; <see langword="null"/> for the ruleset's default.</param>
/// <param name="Map">
/// A predefined map from the map pool, validated against the same ruleset, to play on instead of a generated one; its size wins
/// over <paramref name="Width"/> and <paramref name="Height"/>. <see langword="null"/> generates a map.
/// </param>
public sealed record GameSetup(ImmutableArray<PlayerId> Players, ulong Seed, int? Width = null, int? Height = null, PredefinedMap? Map = null);
