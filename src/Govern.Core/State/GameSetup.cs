using System.Collections.Immutable;

namespace Govern.Core.State;

/// <summary>
/// Everything needed to start a game with <see cref="GameFactory.Create"/>. The host validates user input before building it;
/// an invalid setup is a programming error and throws.
/// </summary>
/// <param name="Players">Distinct players, in turn order; as many as the ruleset's <see cref="Rulesets.SetupRules.Players"/>.</param>
/// <param name="Seed">Seed for spawn positions (and any later randomness).</param>
/// <param name="Width">Map columns within the ruleset's map size limits; <see langword="null"/> for the ruleset's default.</param>
/// <param name="Height">Map rows within the ruleset's map size limits; <see langword="null"/> for the ruleset's default.</param>
public sealed record GameSetup(ImmutableArray<PlayerId> Players, ulong Seed, int? Width = null, int? Height = null);
