using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Rulesets;

// The checks on the ruleset's map section: size limits, the sizes per player count, the default terrain, the spawn zones and
// the generator's patches.
internal static class MapRulesValidator
{
    // The engine's own bounds on the map; they keep a stored game's size bounded whatever a ruleset says.
    private const int SmallestMap = 8;
    private const int LargestMap = 40;

    // At most a quarter of the width each, so the spawn zones on either side, and the units placed towards the centre, never meet.
    private const int MaxZoneColumnsPerMille = 250;

    // The generator keeps at least half of every map default terrain, and lays down few enough impassable tiles that it can
    // always keep the passable ones connected.
    private const int MaxGeneratedSharePerMille = 500;
    private const int MaxImpassableSharePerMille = 150;

    public static void Validate(RulesetDocument document, HashSet<string> terrains, List<RulesetProblem> problems)
    {
        CheckMap(document.Setup, document.Map, terrains, document.Terrains, problems);
        CheckGeneration(document.Map, terrains, document.Terrains, problems);
    }

    private static void CheckMap(SetupRules setup, MapRules map, HashSet<string> terrainIds, ImmutableArray<TerrainDefinition> terrains, List<RulesetProblem> problems)
    {
        RulesetValidator.InRange(map.MinSize, SmallestMap, LargestMap, "$.map.minSize", problems);
        RulesetValidator.InRange(map.MaxSize, Math.Max(map.MinSize, SmallestMap), LargestMap, "$.map.maxSize", problems);
        CheckSizes(setup, map, problems);

        if (RulesetValidator.Reference(map.DefaultTerrain.Id, terrainIds, "terrain", "$.map.defaultTerrain", problems)
            && !terrains.First(terrain => terrain.Id == map.DefaultTerrain).IsPassable)
        {
            problems.Add(new("$.map.defaultTerrain", "Must be passable: every unit starts on it."));
        }

        RulesetValidator.InRange(map.Spawn.ZoneColumnsPerMille, 1, MaxZoneColumnsPerMille, "$.map.spawn.zoneColumnsPerMille", problems);
        var zone = map.Spawn.ZoneColumns(map.MinSize);
        if (map.Spawn.EdgeMargin < 1 || map.Spawn.EdgeMargin >= zone || map.Spawn.EdgeMargin * 2 >= map.MinSize)
        {
            problems.Add(new("$.map.spawn.edgeMargin", $"Must be at least 1 and less than the spawn zone on the smallest map ({zone} columns)."));
        }
    }

    // One size per player count the setup allows, each within the size limits. Corner spawn zones (a third of the height) always
    // have rows: the edge margin is below a quarter of the smallest map.
    private static void CheckSizes(SetupRules setup, MapRules map, List<RulesetProblem> problems)
    {
        for (var index = 0; index < map.Sizes.Length; index++)
        {
            var size = map.Sizes[index];
            var path = $"$.map.sizes[{index}]";
            if (!setup.Allows(size.Players) || map.Sizes.Count(other => other.Players == size.Players) > 1)
            {
                problems.Add(new($"{path}.players", "Must be a player count the setup allows, listed once."));
            }

            RulesetValidator.InRange(size.Width, map.MinSize, map.MaxSize, $"{path}.width", problems);
            RulesetValidator.InRange(size.Height, map.MinSize, map.MaxSize, $"{path}.height", problems);
        }

        for (var players = setup.MinPlayers; players <= setup.MaxPlayers && players <= RulesetValidator.MostPlayers; players++)
        {
            if (!map.Sizes.Any(size => size.Players == players))
            {
                problems.Add(new("$.map.sizes", $"A size for {players} players is required."));
            }
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
            if (RulesetValidator.Reference(patch.Terrain.Id, terrainIds, "terrain", $"{path}.terrain", problems)
                && (patch.Terrain == map.DefaultTerrain || !seen.Add(patch.Terrain.Id)))
            {
                problems.Add(new($"{path}.terrain", "Must not be the default terrain or a terrain listed before."));
            }

            RulesetValidator.AtLeast(patch.SharePerMille, 1, $"{path}.sharePerMille", problems);
            RulesetValidator.AtLeast(patch.PatchSize, 1, $"{path}.patchSize", problems);
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
}
