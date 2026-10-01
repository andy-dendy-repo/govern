using System.Collections.Immutable;
using Govern.Core.Economy;

namespace Govern.Core.Rulesets;

// The cost and upkeep checks of RulesetValidator (via EconomyRulesValidator): units' and buildings' resource costs and upkeep,
// and the upkeep section. Problems go into the same list, with JSON paths.
internal static class UpkeepRulesValidator
{
    private const int MaxSupplyRadius = 3;

    public static void Validate(RulesetDocument document, HashSet<string> resources, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            CheckCost(document, unit.ResourceCost, resources, $"$.units[{index}].resourceCost", problems);
            EconomyRulesValidator.CheckYields(unit.Upkeep, resources, $"$.units[{index}].upkeep", problems);

            // A unit sees every tile that supplies it and the rivers on their edges, so the supply preview is exact.
            if (!unit.Upkeep.IsEmpty && unit.Sight < document.Upkeep.SupplyRadius + 1)
            {
                problems.Add(new($"$.units[{index}].sight", $"Must be at least upkeep.supplyRadius + 1 ({document.Upkeep.SupplyRadius + 1}), so the unit sees the land that supplies it."));
            }
        }

        for (var index = 0; index < document.Buildings.Length; index++)
        {
            var building = document.Buildings[index];
            CheckCost(document, building.ResourceCost, resources, $"$.buildings[{index}].resourceCost", problems);
            EconomyRulesValidator.CheckYields(building.Upkeep, resources, $"$.buildings[{index}].upkeep", problems);
        }

        var upkeep = document.Upkeep;
        RulesetValidator.InRange(upkeep.SupplyRadius, 0, MaxSupplyRadius, "$.upkeep.supplyRadius", problems);
        RulesetValidator.InRange(upkeep.ShortageDamage, 1, Math.Max(1, document.Combat.MaxHealth - 1), "$.upkeep.shortageDamage", problems);
        RulesetValidator.InRange(upkeep.BuildingHealth, 1, EconomyRulesValidator.MaxCost, "$.upkeep.buildingHealth", problems);
        RulesetValidator.InRange(upkeep.BuildingRecovery, 1, Math.Max(1, upkeep.BuildingHealth), "$.upkeep.buildingRecovery", problems);
    }

    private static void CheckCost(RulesetDocument document, ImmutableArray<ResourceAmount> cost, HashSet<string> resources, string path, List<RulesetProblem> problems)
    {
        EconomyRulesValidator.CheckYields(cost, resources, path, problems, EconomyRulesValidator.MaxStock);

        // Territory growth spends this resource just before production pays, and which tile it buys can depend on tiles the
        // player doesn't see; keeping it out of costs lets the city panel tell exactly whether production can begin.
        if (cost.Any(amount => amount.Resource == document.Territory.GrowthResource))
        {
            problems.Add(new(path, $"Can't use '{document.Territory.GrowthResource.Id}', which pays for territory."));
        }
    }
}
