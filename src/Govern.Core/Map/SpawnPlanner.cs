using System.Collections.Immutable;
using Govern.Core.Engine;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

// Spawns on a generated map for three or four players, chosen after the terrain. A generated map is a mirror through its centre,
// which pairs opposite corners but not neighbouring ones, so fairness is measured instead, as for a predefined map: every zone's
// candidates are the tiles whose row of starting units stands on passable land. The target is the start value every zone can come
// closest to, and each zone draws only among its candidates within SideTolerancePercent of it (or, when it has none, its closest
// ones), so the starts drawn are worth about the same.
internal static class SpawnPlanner
{
    // Picks one spawn per player, in seat order, with hashed draws. A zone without a single candidate (possible only on a crowded
    // small map) gets its first tile's row cleared to the default terrain, so the starting units always have ground.
    public static (GameMap Map, ImmutableArray<HexCoord> Spawns) Place(Ruleset ruleset, GameMap map, DeterministicRandom random, int players)
    {
        var zones = Zones(ruleset.Map.Spawn, map, players);
        var candidates = new List<List<HexCoord>>();
        foreach (var zone in zones)
        {
            var found = zone.Where(spawn => Row(ruleset, map, spawn).All(tile => map.IsPassable(ruleset, tile))).ToList();
            if (found.Count == 0)
            {
                map = Clear(map, Row(ruleset, map, zone[0]), ruleset.Map.DefaultTerrain);
                found = [zone[0]];
            }

            candidates.Add(found);
        }

        var values = candidates.Select(zone => zone.Select(spawn => (Spawn: spawn, Value: PredefinedMapBalanceValidator.StartValue(ruleset, map, spawn))).ToList()).ToList();
        var target = Target(values);
        var spawns = values.Select(zone =>
        {
            var fair = Fair(zone, target);
            return fair[random.NextHashed(0, fair.Count)];
        });
        return (map, [.. spawns]);
    }

    // The tiles of each player's zone, in seat order, row by row. Three players: the left edge, then the top and bottom thirds of
    // the right edge; four: the corners clockwise from the top left. Each zone is the spawn zone's width, inside the edge margin.
    public static ImmutableArray<ImmutableArray<HexCoord>> Zones(SpawnRules spawn, GameMap map, int players)
    {
        var columns = spawn.ZoneColumns(map.Width);
        var rows = SpawnRules.CornerRows(map.Height);
        var (left, right) = ((spawn.EdgeMargin, columns), (map.Width - columns, map.Width - spawn.EdgeMargin));
        var (top, bottom, full) = ((spawn.EdgeMargin, rows), (map.Height - rows, map.Height - spawn.EdgeMargin), (spawn.EdgeMargin, map.Height - spawn.EdgeMargin));
        (int, int)[][] boxes = players == 3
            ? [[left, full], [right, top], [right, bottom]]
            : [[left, top], [right, top], [right, bottom], [left, bottom]];
        return [.. boxes.Select(box => Box(box[0], box[1]))];
    }

    // The tiles the starting units stand on: the spawn, then one column further towards the centre per unit.
    public static ImmutableArray<HexCoord> Row(Ruleset ruleset, GameMap map, HexCoord spawn)
    {
        var towardsCentre = PredefinedMapBalanceValidator.TowardsCentre(map, spawn);
        return [.. Enumerable.Range(0, ruleset.Setup.StartingUnits.Length)
            .Select(slot => HexCoord.FromOffset(spawn.Column + (slot * towardsCentre), spawn.Row))];
    }

    private static ImmutableArray<HexCoord> Box((int From, int To) columns, (int From, int To) rows) =>
        [.. Enumerable.Range(rows.From, rows.To - rows.From)
            .SelectMany(row => Enumerable.Range(columns.From, columns.To - columns.From).Select(column => HexCoord.FromOffset(column, row)))];

    // Of all candidates' values, the one whose farthest zone (by that zone's closest candidate) is nearest; ties go to the
    // smaller value, so the choice never depends on order.
    private static int Target(List<List<(HexCoord Spawn, int Value)>> zones) => zones
        .SelectMany(static zone => zone.Select(static candidate => candidate.Value))
        .Distinct()
        .OrderBy(value => zones.Max(zone => zone.Min(candidate => Math.Abs(candidate.Value - value))))
        .ThenBy(static value => value)
        .First();

    // The zone's candidates within the tolerance of the target, in the zone's order; when none is, those closest to it.
    private static List<HexCoord> Fair(List<(HexCoord Spawn, int Value)> zone, int target)
    {
        var tolerance = target * PredefinedMapBalanceValidator.SideTolerancePercent / 100;
        var limit = Math.Max(tolerance, zone.Min(candidate => Math.Abs(candidate.Value - target)));
        return [.. zone.Where(candidate => Math.Abs(candidate.Value - target) <= limit).Select(static candidate => candidate.Spawn)];
    }

    private static GameMap Clear(GameMap map, ImmutableArray<HexCoord> tiles, TerrainType terrain)
    {
        var cleared = tiles.ToHashSet();
        return map with { Tiles = [.. map.Tiles.Select(tile => cleared.Contains(tile.Coord) ? tile with { Terrain = terrain } : tile)] };
    }
}
