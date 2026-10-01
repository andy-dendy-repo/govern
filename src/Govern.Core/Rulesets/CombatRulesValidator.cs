using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The combat checks of RulesetValidator: unit strengths, ranged attacks and the combat section. Kept apart so each validator
// stays small; problems go into the same list, with JSON paths.
internal static class CombatRulesValidator
{
    private const int MaxSpreadPercent = 99;

    // Upper bounds keep the integer combat maths (strength × health, damage × roll percent) far from overflowing.
    private const int MaxStat = 1000;
    private const int MaxBonus = 100;

    public static void Validate(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Units.Length; index++)
        {
            CheckUnit(document.Units[index], $"$.units[{index}]", problems);
        }

        var combat = document.Combat;
        RulesetValidator.InRange(combat.MaxHealth, 1, MaxStat, "$.combat.maxHealth", problems);
        CheckDamage(combat.Damage, problems);
        RulesetValidator.InRange(combat.Healing.PerTurn, 0, MaxStat, "$.combat.healing.perTurn", problems);
        RulesetValidator.InRange(combat.Healing.InOwnCity, 0, MaxStat, "$.combat.healing.inOwnCity", problems);

        var classes = document.Units.Select(static unit => unit.Class).ToHashSet(StringComparer.Ordinal);
        CheckCounters(combat, classes, problems);
        foreach (var key in classes.Order(StringComparer.Ordinal).Select(static c => $"class.{c}.name").Where(key => !document.Texts.ContainsKey(key)))
        {
            problems.Add(new("$.texts", $"The text '{key}' is missing."));
        }
    }

    private static void CheckUnit(UnitDefinition unit, string path, List<RulesetProblem> problems)
    {
        if (!RulesetValidator.IsValidId(unit.Class))
        {
            problems.Add(new($"{path}.class", $"'{unit.Class}' must be lower snake_case."));
        }

        if (unit.Strength is { } strength)
        {
            RulesetValidator.InRange(strength, 1, MaxStat, $"{path}.strength", problems);
        }
        else if (unit.CanAttack || unit.CanRangedAttack)
        {
            problems.Add(new($"{path}.strength", "A unit that can attack needs a strength."));
        }

        if (unit.CanAttack && unit.CanRangedAttack)
        {
            problems.Add(new($"{path}.abilities", "A unit attacks either in melee or at range, not both."));
        }

        if (unit.CanRangedAttack != unit.Ranged is not null)
        {
            problems.Add(new($"{path}.ranged", "Must be set exactly when the unit has the ranged_attack ability."));
        }

        if (unit.Ranged is { } ranged)
        {
            RulesetValidator.InRange(ranged.Strength, 1, MaxStat, $"{path}.ranged.strength", problems);
            RulesetValidator.InRange(ranged.Range, 1, Math.Max(1, unit.Sight), $"{path}.ranged.range", problems);
        }
    }

    private static void CheckDamage(DamageRules damage, List<RulesetProblem> problems)
    {
        RulesetValidator.InRange(damage.PerStrengthPoint, 0, MaxBonus, "$.combat.damage.perStrengthPoint", problems);
        RulesetValidator.InRange(damage.Min, 1, MaxStat, "$.combat.damage.min", problems);
        RulesetValidator.InRange(damage.Max, damage.Min, Math.Max(damage.Min, MaxStat), "$.combat.damage.max", problems);
        RulesetValidator.InRange(damage.Base, damage.Min, Math.Max(damage.Min, damage.Max), "$.combat.damage.base", problems);
        RulesetValidator.InRange(damage.SpreadPercent, 0, MaxSpreadPercent, "$.combat.damage.spreadPercent", problems);
    }

    private static void CheckCounters(CombatRules combat, HashSet<string> classes, List<RulesetProblem> problems)
    {
        var pairs = new HashSet<(string, string)>();
        for (var index = 0; index < combat.Counters.Length; index++)
        {
            var counter = combat.Counters[index];
            var path = $"$.combat.counters[{index}]";
            UsedClass(counter.Class, classes, $"{path}.class", problems);
            UsedClass(counter.Against, classes, $"{path}.against", problems);
            RulesetValidator.InRange(counter.Bonus, 1, MaxBonus, $"{path}.bonus", problems);
            if (!pairs.Add((counter.Class, counter.Against)))
            {
                problems.Add(new(path, "The same class and opponent are listed twice."));
            }
        }
    }

    private static void UsedClass(string unitClass, HashSet<string> classes, string path, List<RulesetProblem> problems)
    {
        if (!classes.Contains(unitClass))
        {
            problems.Add(new(path, $"No unit has the class '{unitClass}'."));
        }
    }
}
