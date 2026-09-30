using System.Collections.Immutable;

namespace Govern.Core.Rulesets;

/// <summary>
/// Health, damage, healing and class counters (the ruleset's <c>combat</c> section).
/// </summary>
/// <param name="MaxHealth">Health of every unit when unhurt; a unit at 0 is destroyed.</param>
/// <param name="Damage">How much damage an attack deals.</param>
/// <param name="Healing">How much a resting unit heals.</param>
/// <param name="Counters">Strength bonuses one unit class gets against another, attacking or defending.</param>
public sealed record CombatRules(int MaxHealth, DamageRules Damage, HealingRules Healing, ImmutableArray<CounterRules> Counters);
