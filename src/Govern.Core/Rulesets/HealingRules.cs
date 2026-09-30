namespace Govern.Core.Rulesets;

/// <summary>
/// Healing (the ruleset's <c>combat.healing</c>): at the end of its owner's turn, a hurt unit that neither moved nor attacked
/// heals, up to <see cref="CombatRules.MaxHealth"/>.
/// </summary>
/// <param name="PerTurn">Health a resting unit heals.</param>
/// <param name="InOwnCity">Health a resting unit heals in one of its owner's cities.</param>
public sealed record HealingRules(int PerTurn, int InOwnCity);
