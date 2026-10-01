using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;

namespace Govern.Core.Engine;

// Strengths and damage ranges of a fight. Integer maths only, so the server and the browser always agree; AttackRule rolls
// within the ranges this gives, and the preview shows them (GameEnginePreviewTests checks the parity).
internal static class CombatCalculator
{
    private const int Percent = 100;

    // river: the river on the edge between the two units, if any; it helps the defender against a melee attack only.
    public static CombatForecast Forecast(Ruleset ruleset, Combatant attacker, Combatant defender, RiverType? river)
    {
        var attackerUnit = ruleset.Unit(attacker.Type);
        var defenderUnit = ruleset.Unit(defender.Type);
        var isRanged = attackerUnit.CanRangedAttack;
        var attackerBase = isRanged ? attackerUnit.Ranged!.Strength : attackerUnit.Strength ?? 0;
        var attackerSide = Side(ruleset, attackerBase, attacker, defenderUnit.Class, defence: []);

        if (defenderUnit.Strength is not { } defenderBase)
        {
            // A civilian never fights: it is destroyed and does no damage back.
            var civilian = new CombatSide(0, 0, [], defender.Health, defender.Health, defender.Health);
            return new CombatForecast(attackerSide, civilian, isRanged);
        }

        var crossed = isRanged || river is not { } type ? null : ruleset.River(type);
        var defenderSide = Side(ruleset, defenderBase, defender, attackerUnit.Class, Defence(ruleset.Terrain(defender.Terrain), crossed));
        var damage = ruleset.Combat.Damage;
        var dealt = RawDamage(damage, attackerSide.Strength, defenderSide.Strength);
        var taken = isRanged ? 0 : RawDamage(damage, defenderSide.Strength, attackerSide.Strength);
        return new CombatForecast(WithDamage(attackerSide, damage, taken), WithDamage(defenderSide, damage, dealt), isRanged);
    }

    // The damage before the random roll: base + strength difference × per point, within [min, max].
    public static int RawDamage(DamageRules damage, int dealerStrength, int receiverStrength) =>
        Math.Clamp(damage.Base + ((dealerStrength - receiverStrength) * damage.PerStrengthPoint), damage.Min, damage.Max);

    // The damage for one roll (a percentage within 100 ± spread), at least 1 and at most the receiver's health; 0 when none.
    public static int Damage(int raw, int rollPercent, int health) =>
        raw == 0 ? 0 : Math.Min(health, Math.Max(1, raw * rollPercent / Percent));

    // The roll's percentage range, for DeterministicRandom.NextHashed.
    public static (int Min, int MaxExclusive) RollRange(DamageRules damage) =>
        (Percent - damage.SpreadPercent, Percent + damage.SpreadPercent + 1);

    private static CombatSide WithDamage(CombatSide side, DamageRules damage, int raw)
    {
        var (min, maxExclusive) = RollRange(damage);
        return side with { MinDamage = Damage(raw, min, side.Health), MaxDamage = Damage(raw, maxExclusive - 1, side.Health) };
    }

    // The defender's own bonuses: its tile's terrain, then the river the attack crosses.
    private static ImmutableArray<CombatModifier> Defence(TerrainDefinition terrain, RiverDefinition? river)
    {
        var modifiers = ImmutableArray.CreateBuilder<CombatModifier>();
        if (terrain.DefenceBonus > 0)
        {
            modifiers.Add(new CombatModifier(CombatModifierKind.Terrain, terrain.DefenceBonus, terrain.Id.Id));
        }

        if (river is { DefenceBonus: > 0 })
        {
            modifiers.Add(new CombatModifier(CombatModifierKind.River, river.DefenceBonus, river.Id.Id));
        }

        return modifiers.ToImmutable();
    }

    // Health first (a hurt unit fights at strength × (max + health) / (2 × max)), then counters, then the defence bonuses.
    private static CombatSide Side(Ruleset ruleset, int baseStrength, Combatant self, string opponentClass, ImmutableArray<CombatModifier> defence)
    {
        var modifiers = ImmutableArray.CreateBuilder<CombatModifier>();
        var maxHealth = ruleset.Combat.MaxHealth;
        var healthLoss = baseStrength - (baseStrength * (maxHealth + self.Health) / (2 * maxHealth));
        if (healthLoss > 0)
        {
            modifiers.Add(new CombatModifier(CombatModifierKind.Health, -healthLoss, null));
        }

        var ownClass = ruleset.Unit(self.Type).Class;
        foreach (var counter in ruleset.Combat.Counters.Where(counter => counter.Class == ownClass && counter.Against == opponentClass))
        {
            modifiers.Add(new CombatModifier(CombatModifierKind.Counter, counter.Bonus, opponentClass));
        }

        modifiers.AddRange(defence);

        var strength = Math.Max(0, baseStrength + modifiers.Sum(static modifier => modifier.Value));
        return new CombatSide(baseStrength, strength, modifiers.ToImmutable(), self.Health, 0, 0);
    }
}
