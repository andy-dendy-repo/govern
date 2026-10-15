using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The checks a parsed document must pass before Core plays with it. Every problem is collected, with its JSON path, so an
// author sees them all at once.
internal static class RulesetValidator
{
    private const int MaxIdLength = 32;
    // A game has at least two players and at most four: the client has four seat colours.
    internal const int FewestPlayers = 2;
    internal const int MostPlayers = 4;

    // Keeps the integer combat maths far from overflowing (see CombatRulesValidator).
    private const int MaxDefenceBonus = 100;

    public static ImmutableArray<RulesetProblem> Validate(RulesetDocument document)
    {
        var problems = new List<RulesetProblem>();
        var units = CheckIds(document.Units.Select(static unit => unit.Id.Id), "$.units", problems);
        var terrains = CheckIds(document.Terrains.Select(static terrain => terrain.Id.Id), "$.terrains", problems);
        var rivers = CheckIds(document.Rivers.Select(static river => river.Id.Id), "$.rivers", problems);

        CheckUnits(document, problems);
        CheckTerrains(document.Terrains, problems);
        CheckSetup(document, units, problems);
        MapRulesValidator.Validate(document, terrains, problems);
        CheckCities(document, units, problems);
        CheckFogInvariants(document, problems);
        CheckTexts(document, problems);
        CombatRulesValidator.Validate(document, problems);
        RiverRulesValidator.Validate(document, rivers, terrains, problems);
        EconomyRulesValidator.Validate(document, terrains, problems);
        ScienceRulesValidator.Validate(document, problems);
        NavalRulesValidator.Validate(document, problems);

        return [.. problems];
    }

    // Valid, unique ids (at least one unless not required); returns the set of them for reference checks.
    internal static HashSet<string> CheckIds(IEnumerable<string> ids, string path, List<RulesetProblem> problems, bool required = true)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var id in ids)
        {
            if (!IsValidId(id))
            {
                problems.Add(new($"{path}[{index}].id", $"'{id}' must be lower snake_case: a letter, then letters, digits or '_', at most {MaxIdLength} characters."));
            }
            else if (!seen.Add(id))
            {
                problems.Add(new($"{path}[{index}].id", $"'{id}' is used twice."));
            }

            index++;
        }

        if (index == 0 && required)
        {
            problems.Add(new(path, "At least one entry is required."));
        }

        return seen;
    }

    internal static bool IsValidId(string id) =>
        id.Length is > 0 and <= MaxIdLength
        && char.IsAsciiLetterLower(id[0])
        && id.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');

    private static void CheckUnits(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            var path = $"$.units[{index}]";
            AtLeast(unit.Movement, 1, $"{path}.movement", problems);
            AtLeast(unit.Sight, 1, $"{path}.sight", problems);
            AtLeast(unit.ProductionCost, 1, $"{path}.productionCost", problems);
            if (unit.Abilities.Distinct().Count() != unit.Abilities.Length)
            {
                problems.Add(new($"{path}.abilities", "An ability is listed twice."));
            }
        }
    }

    private static void CheckTerrains(ImmutableArray<TerrainDefinition> terrains, List<RulesetProblem> problems)
    {
        for (var index = 0; index < terrains.Length; index++)
        {
            AtLeast(terrains[index].MovementCost, 1, $"$.terrains[{index}].movementCost", problems);
            AtLeast(terrains[index].SightBonus, 0, $"$.terrains[{index}].sightBonus", problems);
            InRange(terrains[index].DefenceBonus, 0, MaxDefenceBonus, $"$.terrains[{index}].defenceBonus", problems);
        }

        if (!terrains.IsEmpty && !terrains.Any(static terrain => terrain.IsPassable))
        {
            problems.Add(new("$.terrains", "At least one terrain must be passable."));
        }
    }

    private static void CheckSetup(RulesetDocument document, HashSet<string> units, List<RulesetProblem> problems)
    {
        var setup = document.Setup;
        InRange(setup.MinPlayers, FewestPlayers, MostPlayers, "$.setup.minPlayers", problems);
        InRange(setup.MaxPlayers, Math.Max(setup.MinPlayers, FewestPlayers), MostPlayers, "$.setup.maxPlayers", problems);

        if (setup.StartingUnits.IsEmpty)
        {
            problems.Add(new("$.setup.startingUnits", "At least one starting unit is required."));
            return;
        }

        for (var index = 0; index < setup.StartingUnits.Length; index++)
        {
            Reference(setup.StartingUnits[index].Id, units, "unit", $"$.setup.startingUnits[{index}]", problems);
        }

        if (!setup.StartingUnits.Any(type => document.Units.Any(unit => unit.Id == type && unit.CanFoundCity)))
        {
            problems.Add(new("$.setup.startingUnits", "At least one starting unit must be able to found a city, or players are eliminated at once."));
        }

        var zone = document.Map.Spawn.ZoneColumns(document.Map.MinSize);
        if (setup.StartingUnits.Length > zone)
        {
            problems.Add(new("$.setup.startingUnits", $"At most {zone} units fit in a spawn zone on the smallest map."));
        }
    }

    private static void CheckCities(RulesetDocument document, HashSet<string> units, List<RulesetProblem> problems)
    {
        var cities = document.Cities;
        AtLeast(cities.Sight, 1, "$.cities.sight", problems);
        AtLeast(cities.MinimumSpacing, 1, "$.cities.minimumSpacing", problems);
        AtLeast(cities.ProductionPerTurn, 1, "$.cities.productionPerTurn", problems);
        Reference(cities.InitialProduction.Id, units, "unit", "$.cities.initialProduction", problems);

        if (cities.SuggestedNames.IsDefaultOrEmpty)
        {
            problems.Add(new("$.cities.suggestedNames", "At least one suggested city name is required."));
            return;
        }

        var seen = new List<string>();
        for (var index = 0; index < cities.SuggestedNames.Length; index++)
        {
            var key = cities.SuggestedNames[index];
            if (!document.Texts.TryGetValue(key, out var name))
            {
                problems.Add(new($"$.cities.suggestedNames[{index}]", $"The text '{key}' does not exist."));
            }
            else if (FoundCityRule.CheckName(name, seen) is not null || name != FoundCityRule.NormaliseName(name))
            {
                problems.Add(new($"$.texts['{key}']", "A suggested city name must be a valid city name, used once."));
            }
            else
            {
                seen.Add(name);
            }
        }
    }

    // The view previews are exact only while a unit sees every tile it could reach (over its domain's cheapest ground), and a settler sees every city that
    // blocks founding (see ViewPreviewCalculator).
    private static void CheckFogInvariants(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            var ground = document.Terrains.Where(terrain => terrain.AllowsDomain(unit.Domain) && terrain.MovementCost >= 1).ToList();
            var cheapest = ground.Count == 0 ? 1 : ground.Min(static terrain => terrain.MovementCost);
            if (unit.Sight < unit.Movement / cheapest)
            {
                problems.Add(new($"$.units[{index}].sight", $"Must be at least {unit.Movement / cheapest}, the farthest the unit can move."));
            }

            if (unit.CanFoundCity && unit.Sight < document.Cities.MinimumSpacing - 1)
            {
                problems.Add(new($"$.units[{index}].sight", $"Must be at least {document.Cities.MinimumSpacing - 1} (city spacing - 1), so the unit sees every city that blocks founding."));
            }
        }
    }

    private static void CheckTexts(RulesetDocument document, List<RulesetProblem> problems)
    {
        var required = document.Units.Select(static unit => $"unit.{unit.Id.Id}.name")
            .Concat(document.Terrains.Select(static terrain => $"terrain.{terrain.Id.Id}.name"));
        foreach (var key in required.Where(key => !document.Texts.ContainsKey(key)))
        {
            problems.Add(new("$.texts", $"The text '{key}' is missing."));
        }

        foreach (var (key, _) in document.Texts.Where(static text => string.IsNullOrWhiteSpace(text.Value)).OrderBy(static text => text.Key, StringComparer.Ordinal))
        {
            problems.Add(new($"$.texts['{key}']", "A text must not be empty."));
        }
    }

    internal static bool Reference(string id, HashSet<string> ids, string kind, string path, List<RulesetProblem> problems)
    {
        if (ids.Contains(id))
        {
            return true;
        }

        problems.Add(new(path, $"The {kind} '{id}' does not exist."));
        return false;
    }

    internal static void AtLeast(int value, int minimum, string path, List<RulesetProblem> problems)
    {
        if (value < minimum)
        {
            problems.Add(new(path, $"Must be at least {minimum}."));
        }
    }

    internal static void InRange(int value, int minimum, int maximum, string path, List<RulesetProblem> problems)
    {
        if (value < minimum || value > maximum)
        {
            problems.Add(new(path, $"Must be from {minimum} to {maximum}."));
        }
    }
}
