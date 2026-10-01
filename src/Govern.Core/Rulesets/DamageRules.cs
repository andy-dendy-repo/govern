namespace Govern.Core.Rulesets;

/// <summary>
/// The damage formula (the ruleset's <c>combat.damage</c>): <c>Base + (own strength − opponent's strength) × PerStrengthPoint</c>,
/// kept within <c>[Min, Max]</c>, then scaled by a random percentage in <c>100 ± SpreadPercent</c> (at least 1).
/// </summary>
/// <param name="Base">Damage between equally strong units, before the random spread.</param>
/// <param name="PerStrengthPoint">Extra damage per point of strength the dealer has over the receiver (less per point below).</param>
/// <param name="Min">The least damage before the random spread (at least 1).</param>
/// <param name="Max">The most damage before the random spread.</param>
/// <param name="SpreadPercent">How far, in percent, the random roll moves the damage up or down (0 to 99).</param>
public sealed record DamageRules(int Base, int PerStrengthPoint, int Min, int Max, int SpreadPercent);
