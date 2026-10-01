using System.Collections.Immutable;

namespace Govern.Core.Visibility;

/// <summary>
/// One side of a <see cref="CombatForecast"/>.
/// </summary>
/// <param name="BaseStrength">The unit's strength from the ruleset (its ranged strength when it shoots); 0 for a civilian.</param>
/// <param name="Strength">Strength in this fight: <paramref name="BaseStrength"/> plus every modifier, at least 0.</param>
/// <param name="Modifiers">What changes the strength, in the order health, counters, terrain; empty for a civilian.</param>
/// <param name="Health">Health before the fight.</param>
/// <param name="MinDamage">The least health this side can lose (0 when it takes no damage).</param>
/// <param name="MaxDamage">The most health this side can lose; equal to <paramref name="Health"/> when it may be destroyed.</param>
public sealed record CombatSide(int BaseStrength, int Strength, ImmutableArray<CombatModifier> Modifiers, int Health, int MinDamage, int MaxDamage)
{
    /// <summary>Whether this side may be destroyed.</summary>
    public bool MayBeDestroyed => MaxDamage >= Health;

    /// <summary>Whether this side is sure to be destroyed.</summary>
    public bool IsDestroyed => MinDamage >= Health;
}
