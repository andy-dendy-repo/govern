using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Rulesets;

// The building checks of RulesetValidator: resources, tile improvements, city buildings, bridges, the units that build and the
// cities' work areas. Kept apart so each validator stays small; problems go into the same list, with JSON paths.
internal static class EconomyRulesValidator
{
    // Keeps per-turn sums far from overflowing over a long game.
    private const int MaxCost = 1000;
    private const int MaxYield = 100;
    private const int MaxWorkRadius = 5;

    public static void Validate(RulesetDocument document, HashSet<string> terrains, List<RulesetProblem> problems)
    {
        var resources = RulesetValidator.CheckIds(document.Resources.Select(static resource => resource.Id.Id), "$.resources", problems);
        RulesetValidator.CheckIds(document.Improvements.Select(static improvement => improvement.Id.Id), "$.improvements", problems, required: false);
        RulesetValidator.CheckIds(document.Buildings.Select(static building => building.Id.Id), "$.buildings", problems, required: false);

        CheckImprovements(document, terrains, resources, problems);
        CheckBuildings(document, terrains, resources, problems);
        RulesetValidator.InRange(document.Bridges.Cost, 1, MaxCost, "$.bridges.cost", problems);
        CheckBuilders(document, problems);
        CheckWorkArea(document.Cities, problems);
        CheckTexts(document, problems);
    }

    private static void CheckImprovements(RulesetDocument document, HashSet<string> terrains, HashSet<string> resources, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Improvements.Length; index++)
        {
            var improvement = document.Improvements[index];
            var path = $"$.improvements[{index}]";
            RulesetValidator.InRange(improvement.Cost, 1, MaxCost, $"{path}.cost", problems);
            CheckYields(improvement.Yields, resources, $"{path}.yields", problems);
            if (improvement.Terrains.IsEmpty)
            {
                problems.Add(new($"{path}.terrains", "At least one terrain is required."));
            }

            CheckTerrains(improvement.Terrains, terrains, $"{path}.terrains", problems);

            // A builder stands on the tile it builds on.
            foreach (var terrain in improvement.Terrains.Where(terrain => document.Terrains.FirstOrDefault(known => known.Id == terrain) is { IsPassable: false }))
            {
                problems.Add(new($"{path}.terrains", $"The terrain '{terrain.Id}' is impassable, so no builder can stand on it."));
            }
        }
    }

    private static void CheckBuildings(RulesetDocument document, HashSet<string> terrains, HashSet<string> resources, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Buildings.Length; index++)
        {
            var building = document.Buildings[index];
            var path = $"$.buildings[{index}]";
            RulesetValidator.InRange(building.Cost, 1, MaxCost, $"{path}.cost", problems);
            RulesetValidator.InRange(building.Production, 0, MaxYield, $"{path}.production", problems);
            CheckYields(building.Yields, resources, $"{path}.yields", problems);
            CheckTerrains(building.RequiresAdjacentTerrain, terrains, $"{path}.requiresAdjacentTerrain", problems);
        }
    }

    // A unit builds exactly when it has a build ability and its own build production.
    private static void CheckBuilders(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            var path = $"$.units[{index}]";
            if (unit.CanBuildImprovements || unit.CanBuildBridges)
            {
                RulesetValidator.InRange(unit.BuildProduction, 1, MaxYield, $"{path}.buildProduction", problems);
            }
            else if (unit.BuildProduction != 0)
            {
                problems.Add(new($"{path}.buildProduction", "Must be 0 (or left out) for a unit that can't build."));
            }

            // A bridge is refused next to an enemy city's work area, so the unit must see every city that could cover the far
            // side of the river, or the preview could differ from the rule.
            if (unit.CanBuildBridges && unit.Sight < document.Cities.WorkRadius + 1)
            {
                problems.Add(new($"{path}.sight", $"Must be at least {document.Cities.WorkRadius + 1} (work radius + 1), so the unit sees every work area a bridge could touch."));
            }
        }
    }

    private static void CheckWorkArea(CityRules cities, List<RulesetProblem> problems)
    {
        RulesetValidator.InRange(cities.WorkRadius, 1, MaxWorkRadius, "$.cities.workRadius", problems);
        if (cities.MinimumSpacing <= 2 * cities.WorkRadius)
        {
            problems.Add(new("$.cities.workRadius", $"Must be less than half of minimumSpacing ({cities.MinimumSpacing}), so two work areas never overlap."));
        }
    }

    private static void CheckYields(ImmutableArray<ResourceAmount> yields, HashSet<string> resources, string path, List<RulesetProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < yields.Length; index++)
        {
            var amount = yields[index];
            if (!resources.Contains(amount.Resource.Id))
            {
                problems.Add(new($"{path}[{index}].resource", $"The resource '{amount.Resource.Id}' does not exist."));
            }
            else if (!seen.Add(amount.Resource.Id))
            {
                problems.Add(new($"{path}[{index}].resource", $"'{amount.Resource.Id}' is listed twice."));
            }

            RulesetValidator.InRange(amount.Amount, 1, MaxYield, $"{path}[{index}].amount", problems);
        }
    }

    private static void CheckTerrains(ImmutableArray<TerrainType> list, HashSet<string> terrains, string path, List<RulesetProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < list.Length; index++)
        {
            if (!terrains.Contains(list[index].Id))
            {
                problems.Add(new($"{path}[{index}]", $"The terrain '{list[index].Id}' does not exist."));
            }
            else if (!seen.Add(list[index].Id))
            {
                problems.Add(new($"{path}[{index}]", $"'{list[index].Id}' is listed twice."));
            }
        }
    }

    private static void CheckTexts(RulesetDocument document, List<RulesetProblem> problems)
    {
        var required = document.Resources.Select(static resource => $"resource.{resource.Id.Id}.name")
            .Concat(document.Improvements.Select(static improvement => $"improvement.{improvement.Id.Id}.name"))
            .Concat(document.Buildings.Select(static building => $"building.{building.Id.Id}.name"));
        foreach (var key in required.Where(key => !document.Texts.ContainsKey(key)))
        {
            problems.Add(new("$.texts", $"The text '{key}' is missing."));
        }
    }
}
