using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The science checks of RulesetValidator: the science section (its resource and nodes), unit lines (replaces, upgradeCost) and
// building upgrades. Problems go into the same list, with JSON paths.
internal static class ScienceRulesValidator
{
    private const int MaxPoints = 100;

    public static void Validate(RulesetDocument document, List<RulesetProblem> problems)
    {
        CheckResource(document, problems);
        CheckNodes(document, problems);
        CheckUnitLines(document, problems);
        CheckBuildingUpgrades(document, problems);
    }

    // Science is a budget, never stored, spent, traded or used to grow territory.
    private static void CheckResource(RulesetDocument document, List<RulesetProblem> problems)
    {
        var science = document.Science.Resource;
        if (document.Resources.FirstOrDefault(resource => resource.Id == science) is not { } definition)
        {
            problems.Add(new("$.science.resource", $"The resource '{science.Id}' does not exist."));
            return;
        }

        var used = document.Units.SelectMany(static unit => unit.ResourceCost.Concat(unit.Upkeep).Concat(unit.UpgradeCost ?? []))
            .Concat(document.Buildings.SelectMany(static building => building.ResourceCost.Concat(building.Upkeep).Concat(building.Storage)))
            .Concat(document.Cities.StartingStock)
            .Concat(document.Cities.Storage)
            .Any(amount => amount.Resource == science);
        if (definition.IsTradable || used || document.Territory.GrowthResource == science)
        {
            problems.Add(new("$.science.resource", $"'{science.Id}' is science per turn: it can't be traded, stocked, paid or grow territory."));
        }
    }

    private static void CheckNodes(RulesetDocument document, List<RulesetProblem> problems)
    {
        var nodes = document.Science.Nodes;
        RulesetValidator.CheckIds(nodes.Select(static node => node.Id), "$.science.nodes", problems, required: false);
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var unlocked = new HashSet<string>(StringComparer.Ordinal);
        var known = document.Units.Select(static unit => $"unit:{unit.Id.Id}")
            .Concat(document.Buildings.Select(static building => $"building:{building.Id.Id}"))
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            var path = $"$.science.nodes[{index}]";
            RulesetValidator.InRange(node.Points, 1, MaxPoints, $"{path}.points", problems);
            foreach (var required in node.Requires.Where(required => !listed.Contains(required)))
            {
                problems.Add(new($"{path}.requires", $"'{required}' must be a node listed before this one."));
            }

            CheckUnlocks(node.Units.Select(static unit => $"unit:{unit.Id}"), $"{path}.units", known, unlocked, problems);
            CheckUnlocks(node.Buildings.Select(static building => $"building:{building.Id}"), $"{path}.buildings", known, unlocked, problems);
            if (!document.Texts.ContainsKey($"science.{node.Id}.name"))
            {
                problems.Add(new("$.texts", $"The text 'science.{node.Id}.name' is missing."));
            }

            listed.Add(node.Id);
        }

        // Players start with these, before they own any node.
        var always = document.Setup.StartingUnits.Append(document.Cities.InitialProduction).Select(static unit => unit.Id);
        foreach (var unit in always.Where(unit => unlocked.Contains($"unit:{unit}")).Distinct())
        {
            problems.Add(new("$.science.nodes", $"The unit '{unit}' starts the game, so no node may lock it."));
        }
    }

    // Each unit or building exists and is unlocked by one node at most; keys are "kind:id" (unit:soldier, building:campus).
    private static void CheckUnlocks(IEnumerable<string> keys, string path, HashSet<string> known, HashSet<string> unlocked, List<RulesetProblem> problems)
    {
        foreach (var key in keys)
        {
            var separator = key.IndexOf(':', StringComparison.Ordinal);
            var (kind, id) = (key[..separator], key[(separator + 1)..]);
            if (!known.Contains(key))
            {
                problems.Add(new(path, $"The {kind} '{id}' does not exist."));
            }
            else if (!unlocked.Add(key))
            {
                problems.Add(new(path, $"The {kind} '{id}' is unlocked by another node too."));
            }
        }
    }

    private static void CheckUnitLines(RulesetDocument document, List<RulesetProblem> problems)
    {
        var replaced = new HashSet<UnitType>();
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            var path = $"$.units[{index}]";
            if (unit.Replaces is not { } old)
            {
                if (unit.UpgradeCost is not null)
                {
                    problems.Add(new($"{path}.upgradeCost", "Only a unit that replaces another has an upgrade cost."));
                }

                continue;
            }

            if (document.Units.FirstOrDefault(other => other.Id == old) is not { } previous || old == unit.Id)
            {
                problems.Add(new($"{path}.replaces", $"'{old.Id}' must be another unit."));
            }
            else if (previous.Class != unit.Class)
            {
                problems.Add(new($"{path}.replaces", $"'{old.Id}' must be of the same class ('{unit.Class}')."));
            }
            else if (!replaced.Add(old))
            {
                problems.Add(new($"{path}.replaces", $"'{old.Id}' is replaced by another unit too."));
            }

            CheckUpgradeCost(document, unit, $"{path}.upgradeCost", problems);
        }

        CheckNoCycle(document.Units.Select(static unit => (unit.Id.Id, unit.Replaces?.Id)), "$.units", "replaces", problems);
    }

    private static void CheckUpgradeCost(RulesetDocument document, UnitDefinition unit, string path, List<RulesetProblem> problems)
    {
        if (unit.UpgradeCost is not { } cost)
        {
            problems.Add(new(path, "A unit that replaces another needs an upgrade cost (it may be empty)."));
            return;
        }

        EconomyRulesValidator.CheckYields(cost, [.. document.Resources.Select(static resource => resource.Id.Id)], path, problems, EconomyRulesValidator.MaxStock);
    }

    private static void CheckBuildingUpgrades(RulesetDocument document, List<RulesetProblem> problems)
    {
        var upgraded = new HashSet<BuildingType>();
        for (var index = 0; index < document.Buildings.Length; index++)
        {
            var building = document.Buildings[index];
            if (building.Upgrades is not { } old)
            {
                continue;
            }

            var path = $"$.buildings[{index}].upgrades";
            if (old == building.Id || document.Buildings.All(other => other.Id != old))
            {
                problems.Add(new(path, $"'{old.Id}' must be another building."));
            }
            else if (!upgraded.Add(old))
            {
                problems.Add(new(path, $"'{old.Id}' is upgraded by another building too."));
            }
        }

        if (document.Buildings.FirstOrDefault(building => building.Id == document.Trade.Building) is { Upgrades: not null })
        {
            problems.Add(new("$.trade.building", "Must be a building a city produces, not an upgrade."));
        }

        CheckNoCycle(document.Buildings.Select(static building => (building.Id.Id, building.Upgrades?.Id)), "$.buildings", "upgrades", problems);
    }

    // Following the links from any entry must end, or a line would loop forever.
    private static void CheckNoCycle(IEnumerable<(string Id, string? Next)> links, string path, string field, List<RulesetProblem> problems)
    {
        var next = links.Where(static link => link.Next is not null).GroupBy(static link => link.Id).ToImmutableDictionary(static group => group.Key, static group => group.First().Next!);
        foreach (var start in next.Keys.Order(StringComparer.Ordinal))
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            for (var current = start; next.TryGetValue(current, out var following); current = following)
            {
                if (!seen.Add(following))
                {
                    problems.Add(new(path, $"The '{field}' links starting at '{start}' form a loop."));
                    break;
                }
            }
        }
    }
}
