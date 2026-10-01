using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Rulesets;

// The economy checks of RulesetValidator: resources, tile, river and city yields, territory, tile improvements, city buildings,
// bridges and the units that build. Kept apart so each validator stays small; problems go into the same list, with JSON paths.
internal static class EconomyRulesValidator
{
    // Keeps per-turn sums far from overflowing over a long game.
    public const int MaxCost = 1000;
    public const int MaxYield = 100;
    private const int MaxRadius = 10;
    public const int MaxStock = 1000;

    public static void Validate(RulesetDocument document, HashSet<string> terrains, List<RulesetProblem> problems)
    {
        var resources = RulesetValidator.CheckIds(document.Resources.Select(static resource => resource.Id.Id), "$.resources", problems);
        RulesetValidator.CheckIds(document.Improvements.Select(static improvement => improvement.Id.Id), "$.improvements", problems, required: false);
        RulesetValidator.CheckIds(document.Buildings.Select(static building => building.Id.Id), "$.buildings", problems, required: false);

        CheckImprovements(document, terrains, resources, problems);
        CheckBuildings(document, terrains, resources, problems);
        RulesetValidator.InRange(document.Bridges.Cost, 1, MaxCost, "$.bridges.cost", problems);
        CheckBuilders(document, problems);
        CheckTileYields(document, resources, problems);
        CheckCityEconomy(document, resources, problems);
        UpkeepRulesValidator.Validate(document, resources, problems);
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
        }
    }

    private static void CheckTileYields(RulesetDocument document, HashSet<string> resources, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Terrains.Length; index++)
        {
            CheckYields(document.Terrains[index].Yields, resources, $"$.terrains[{index}].yields", problems);
        }

        for (var index = 0; index < document.Rivers.Length; index++)
        {
            CheckYields(document.Rivers[index].Yields, resources, $"$.rivers[{index}].yields", problems);
        }
    }

    private static void CheckCityEconomy(RulesetDocument document, HashSet<string> resources, List<RulesetProblem> problems)
    {
        var cities = document.Cities;
        CheckYields(cities.Yields, resources, "$.cities.yields", problems);
        CheckYields(cities.StartingStock, resources, "$.cities.startingStock", problems, MaxStock);

        var territory = document.Territory;
        RulesetValidator.InRange(territory.InitialRadius, 0, MaxRadius, "$.territory.initialRadius", problems);
        RulesetValidator.InRange(territory.MaxRadius, territory.InitialRadius, MaxRadius, "$.territory.maxRadius", problems);
        RulesetValidator.InRange(territory.TileCost, 1, MaxCost, "$.territory.tileCost", problems);
        RulesetValidator.InRange(territory.TileCostIncrease, 0, MaxCost, "$.territory.tileCostIncrease", problems);
        if (!resources.Contains(territory.GrowthResource.Id))
        {
            problems.Add(new("$.territory.growthResource", $"The resource '{territory.GrowthResource.Id}' does not exist."));
        }

        // Founding claims only unowned tiles, but two new cities' first claims should never compete.
        if (cities.MinimumSpacing <= 2 * territory.InitialRadius)
        {
            problems.Add(new("$.territory.initialRadius", $"Must be less than half of cities.minimumSpacing ({cities.MinimumSpacing}), so two new cities' territories never overlap."));
        }
    }

    public static void CheckYields(ImmutableArray<ResourceAmount> yields, HashSet<string> resources, string path, List<RulesetProblem> problems, int max = MaxYield)
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

            RulesetValidator.InRange(amount.Amount, 1, max, $"{path}[{index}].amount", problems);
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
