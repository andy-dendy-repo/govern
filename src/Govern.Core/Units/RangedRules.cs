namespace Govern.Core.Units;

/// <summary>
/// A ranged unit's attack (the <c>ranged</c> object of a ruleset unit with the <c>ranged_attack</c> ability).
/// </summary>
/// <param name="Strength">Strength when it shoots (its <see cref="UnitDefinition.Strength"/> is used when it is attacked).</param>
/// <param name="Range">The farthest target, in hex steps (at least 1, at most the unit's sight).</param>
public sealed record RangedRules(int Strength, int Range);
