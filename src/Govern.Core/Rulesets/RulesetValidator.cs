using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The checks a parsed document must pass before Core plays with it. Every problem is collected, with its JSON path, so an
// author sees them all at once.
internal static class RulesetValidator
{
    // The engine's own bounds on the map; they keep a stored game's size bounded whatever a ruleset says.
    private const int SmallestMap = 8;
    private const int LargestMap = 40;
    private const int MaxIdLength = 32;
    private const int PlayerCount = 2;

    // At most a quarter of the width each, so the two spawn zones, and the units placed towards the centre, never meet.
    private const int MaxZoneColumnsPerMille = 250;

    // The generator keeps at least half of every map default terrain, and lays down few enough impassable tiles that it can
    // always keep the passable ones connected.
    private const int MaxGeneratedSharePerMille = 500;
    private const int MaxImpassableSharePerMille = 150;

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
        CheckMap(document.Map, terrains, document.Terrains, problems);
        CheckGeneration(document.Map, terrains, document.Terrains, problems);
        CheckCities(document, units, problems);
        CheckFogInvariants(document, problems);
        CheckTexts(document, problems);
        CombatRulesValidator.Validate(document, problems);
        RiverRulesValidator.Validate(document, rivers, terrains, problems);
        EconomyRulesValidator.Validate(document, terrains, problems);

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
        if (setup.Players != PlayerCount)
        {
            problems.Add(new("$.setup.players", $"Must be {PlayerCount}: games are for two players for now."));
        }

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

    private static void CheckMap(MapRules map, HashSet<string> terrainIds, ImmutableArray<TerrainDefinition> terrains, List<RulesetProblem> problems)
    {
        InRange(map.MinSize, SmallestMap, LargestMap, "$.map.minSize", problems);
        InRange(map.MaxSize, Math.Max(map.MinSize, SmallestMap), LargestMap, "$.map.maxSize", problems);
        InRange(map.DefaultWidth, map.MinSize, map.MaxSize, "$.map.defaultWidth", problems);
        InRange(map.DefaultHeight, map.MinSize, map.MaxSize, "$.map.defaultHeight", problems);

        if (Reference(map.DefaultTerrain.Id, terrainIds, "terrain", "$.map.defaultTerrain", problems)
            && !terrains.First(terrain => terrain.Id == map.DefaultTerrain).IsPassable)
        {
            problems.Add(new("$.map.defaultTerrain", "Must be passable: every unit starts on it."));
        }

        InRange(map.Spawn.ZoneColumnsPerMille, 1, MaxZoneColumnsPerMille, "$.map.spawn.zoneColumnsPerMille", problems);
        var zone = map.Spawn.ZoneColumns(map.MinSize);
        if (map.Spawn.EdgeMargin < 1 || map.Spawn.EdgeMargin >= zone || map.Spawn.EdgeMargin * 2 >= map.MinSize)
        {
            problems.Add(new("$.map.spawn.edgeMargin", $"Must be at least 1 and less than the spawn zone on the smallest map ({zone} columns)."));
        }
    }

    private static void CheckGeneration(MapRules map, HashSet<string> terrainIds, ImmutableArray<TerrainDefinition> terrains, List<RulesetProblem> problems)
    {
        var patches = map.Generation.Patches;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < patches.Length; index++)
        {
            var patch = patches[index];
            var path = $"$.map.generation.patches[{index}]";
            if (Reference(patch.Terrain.Id, terrainIds, "terrain", $"{path}.terrain", problems)
                && (patch.Terrain == map.DefaultTerrain || !seen.Add(patch.Terrain.Id)))
            {
                problems.Add(new($"{path}.terrain", "Must not be the default terrain or a terrain listed before."));
            }

            AtLeast(patch.SharePerMille, 1, $"{path}.sharePerMille", problems);
            AtLeast(patch.PatchSize, 1, $"{path}.patchSize", problems);
        }

        if (patches.Sum(static patch => patch.SharePerMille) > MaxGeneratedSharePerMille)
        {
            problems.Add(new("$.map.generation.patches", $"The shares must add up to at most {MaxGeneratedSharePerMille}."));
        }

        var impassable = patches
            .Where(patch => terrains.FirstOrDefault(terrain => terrain.Id == patch.Terrain) is { IsPassable: false })
            .Sum(static patch => patch.SharePerMille);
        if (impassable > MaxImpassableSharePerMille)
        {
            problems.Add(new("$.map.generation.patches", $"The shares of impassable terrains must add up to at most {MaxImpassableSharePerMille}."));
        }
    }

    private static void CheckCities(RulesetDocument document, HashSet<string> units, List<RulesetProblem> problems)
    {
        var cities = document.Cities;
        AtLeast(cities.Sight, 1, "$.cities.sight", problems);
        AtLeast(cities.MinimumSpacing, 1, "$.cities.minimumSpacing", problems);
        AtLeast(cities.ProductionPerTurn, 1, "$.cities.productionPerTurn", problems);
        Reference(cities.InitialProduction.Id, units, "unit", "$.cities.initialProduction", problems);

        if (!document.Texts.TryGetValue(cities.NameTemplate, out var template))
        {
            problems.Add(new("$.cities.nameTemplate", $"The text '{cities.NameTemplate}' does not exist."));
        }
        else if (!template.Contains("{n}", StringComparison.Ordinal))
        {
            problems.Add(new($"$.texts['{cities.NameTemplate}']", "A city name template must contain {n}."));
        }
    }

    // The view previews are exact only while a unit sees every tile it could reach, and a settler sees every city that
    // blocks founding (see ViewPreviewCalculator).
    private static void CheckFogInvariants(RulesetDocument document, List<RulesetProblem> problems)
    {
        var passable = document.Terrains.Where(static terrain => terrain.IsPassable && terrain.MovementCost >= 1).ToList();
        var cheapest = passable.Count == 0 ? 1 : passable.Min(static terrain => terrain.MovementCost);

        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
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

    private static bool Reference(string id, HashSet<string> ids, string kind, string path, List<RulesetProblem> problems)
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
