using Govern.Core.Map;

namespace Govern.Core.Rulesets;

// The river checks of RulesetValidator: the river kinds and map.generation.rivers. Kept apart so each validator stays small;
// problems go into the same list, with JSON paths.
internal static class RiverRulesValidator
{
    // Keeps movement sums and the integer combat maths far from overflowing, like the terrain bounds.
    private const int MaxCrossingCost = 100;
    private const int MaxDefenceBonus = 100;

    // Long enough to cross the largest map; a bound keeps generation quick.
    private const int MaxRiverLength = 100;
    private const int MaxRiverCount = 20;

    public static void Validate(RulesetDocument document, HashSet<string> rivers, HashSet<string> terrains, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Rivers.Length; index++)
        {
            var river = document.Rivers[index];
            RulesetValidator.InRange(river.CrossingCost, 0, MaxCrossingCost, $"$.rivers[{index}].crossingCost", problems);
            RulesetValidator.InRange(river.DefenceBonus, 0, MaxDefenceBonus, $"$.rivers[{index}].defenceBonus", problems);
        }

        if (!document.Rivers.IsEmpty && !document.Rivers.Any(static river => river.IsCrossable))
        {
            problems.Add(new("$.rivers", "At least one river must be crossable."));
        }

        foreach (var key in document.Rivers.Select(static river => $"river.{river.Id.Id}.name").Where(key => !document.Texts.ContainsKey(key)))
        {
            problems.Add(new("$.texts", $"The text '{key}' is missing."));
        }

        CheckGeneration(document, rivers, terrains, problems);
    }

    private static void CheckGeneration(RulesetDocument document, HashSet<string> rivers, HashSet<string> terrains, List<RulesetProblem> problems)
    {
        const string path = "$.map.generation.rivers";
        var generation = document.Map.Generation.Rivers;
        RulesetValidator.InRange(generation.Count, 0, MaxRiverCount, $"{path}.count", problems);
        RulesetValidator.InRange(generation.MinLength, 1, MaxRiverLength, $"{path}.minLength", problems);
        RulesetValidator.InRange(generation.MaxLength, Math.Max(generation.MinLength, 1), MaxRiverLength, $"{path}.maxLength", problems);

        CheckTerrains(generation.Sources, terrains, $"{path}.sources", problems);
        CheckTerrains(generation.Mouths, terrains, $"{path}.mouths", problems);

        var stages = generation.Stages;
        if (stages.IsEmpty)
        {
            problems.Add(new($"{path}.stages", "At least one stage is required."));
            return;
        }

        for (var index = 0; index < stages.Length; index++)
        {
            var stage = stages[index];
            var stagePath = $"{path}.stages[{index}]";
            if (!rivers.Contains(stage.River.Id))
            {
                problems.Add(new($"{stagePath}.river", $"The river '{stage.River.Id}' does not exist."));
            }

            if (index == 0 && stage.FromSegment != 0)
            {
                problems.Add(new($"{stagePath}.fromSegment", "The first stage must start at 0."));
            }
            else if (index > 0 && stage.FromSegment <= stages[index - 1].FromSegment)
            {
                problems.Add(new($"{stagePath}.fromSegment", "Must be greater than the stage before."));
            }
        }

        // The generator turns a stretch that would cut the map in two into the last crossable kind before it.
        if (document.Rivers.FirstOrDefault(river => river.Id == stages[0].River) is { IsCrossable: false })
        {
            problems.Add(new($"{path}.stages[0].river", "The first stage must be crossable."));
        }
    }

    private static void CheckTerrains(IReadOnlyList<TerrainType> list, HashSet<string> terrains, string path, List<RulesetProblem> problems)
    {
        if (list.Count == 0)
        {
            problems.Add(new(path, "At least one terrain is required."));
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (!terrains.Contains(list[index].Id))
            {
                problems.Add(new($"{path}[{index}]", $"The terrain '{list[index].Id}' does not exist."));
            }
        }

        if (list.Distinct().Count() != list.Count)
        {
            problems.Add(new(path, "A terrain is listed twice."));
        }
    }
}
