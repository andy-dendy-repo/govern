using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

// The fairness checks on a built predefined map. Its sides need not be mirrors, so fairness is measured: every passable tile is
// reachable from every other without crossing a river that can't be crossed; each player's spawn candidates leave room for the
// starting units on the spawn's side of the centre, as GameFactory places them (the k-th unit k columns towards the centre); on
// a two-player map the two halves yield about as much of every resource; and every spawn candidate starts among land worth
// about the same, for every player.
internal static class PredefinedMapBalanceValidator
{
    // How far apart the halves' yields of one resource, and a spawn candidate's start from the mean of all, may be.
    public const int HalfTolerancePercent = 15;
    public const int StartTolerancePercent = 10;
    public const int SideTolerancePercent = 5;

    // The land a start is judged by: the tiles within this distance of the spawn, the first city's neighbourhood.
    public const int StartRadius = 3;

    public static void Validate(Ruleset ruleset, GameMap map, ImmutableArray<ImmutableArray<HexCoord>> spawns, List<RulesetProblem> problems)
    {
        CheckConnected(ruleset, map, problems);
        for (var group = 0; group < spawns.Length; group++)
        {
            CheckSpawns(ruleset, map, spawns[group], $"$.spawns[{group}]", problems);
        }

        CheckSharedRows(ruleset, map, spawns, problems);

        // With more players the halves hold different numbers of starts, so only the starts themselves are compared.
        if (spawns.Length == 2)
        {
            CheckOppositeHalves(map, spawns, problems);
            CheckHalves(ruleset, map, problems);
        }

        CheckStarts(ruleset, map, spawns, problems);
    }

    /// <summary>The column step from a spawn towards the map's centre column, the way the starting units stand.</summary>
    /// <param name="map">The map.</param>
    /// <param name="spawn">The spawn tile.</param>
    /// <returns>1 in the left half, -1 otherwise.</returns>
    public static int TowardsCentre(GameMap map, HexCoord spawn) => spawn.Column < map.Width / 2 ? 1 : -1;

    // What a start is worth: the yields (every resource alike) of the tiles within StartRadius and of the rivers between them.
    public static int StartValue(Ruleset ruleset, GameMap map, HexCoord spawn)
    {
        var area = spawn.WithinRadius(StartRadius).Where(map.Contains).ToHashSet();
        var tiles = area.Sum(coord => Total(ruleset.Terrain(map.TileAt(coord).Terrain).Yields));
        var rivers = Edges(map)
            .Where(edge => area.Contains(edge.Owner) && area.Contains(edge.Neighbor()))
            .Sum(edge => map.RiverBetween(edge.Owner, edge.Neighbor()) is { } river ? Total(ruleset.River(river).Yields) : 0);
        return tiles + rivers;
    }

    // A flood fill from one passable tile over the steps a land unit can take.
    private static void CheckConnected(Ruleset ruleset, GameMap map, List<RulesetProblem> problems)
    {
        var passable = map.Tiles.Where(tile => ruleset.Terrain(tile.Terrain).IsPassable).Select(static tile => tile.Coord).ToList();
        if (passable.Count == 0)
        {
            problems.Add(new("$.rows", "The map has no passable tile."));
            return;
        }

        var reached = new HashSet<HexCoord> { passable[0] };
        var pending = new Stack<HexCoord>(reached);
        while (pending.TryPop(out var coord))
        {
            foreach (var next in coord.Neighbors())
            {
                if (reached.Contains(next) || !map.IsPassable(ruleset, next))
                {
                    continue;
                }

                if (map.RiverBetween(coord, next) is { } river && !ruleset.River(river).IsCrossable)
                {
                    continue;
                }

                reached.Add(next);
                pending.Push(next);
            }
        }

        if (passable.FirstOrDefault(coord => !reached.Contains(coord)) is var cut && reached.Count != passable.Count)
        {
            problems.Add(new("$.rows", $"Tile {Name(cut)} can't be reached from tile {Name(passable[0])}: lakes, mountains or rivers that can't be crossed cut the map."));
        }
    }

    private static void CheckSpawns(Ruleset ruleset, GameMap map, ImmutableArray<HexCoord> spawns, string path, List<RulesetProblem> problems)
    {
        for (var index = 0; index < spawns.Length; index++)
        {
            var towardsCentre = TowardsCentre(map, spawns[index]);
            var isLeftSide = towardsCentre > 0;
            var half = isLeftSide ? "left" : "right";
            for (var slot = 0; slot < ruleset.Setup.StartingUnits.Length; slot++)
            {
                var tile = HexCoord.FromOffset(spawns[index].Column + (slot * towardsCentre), spawns[index].Row);
                var inHalf = isLeftSide ? IsLeft(map, tile) : IsRight(map, tile);
                if (!map.Contains(tile) || !inHalf || !map.IsPassable(ruleset, tile))
                {
                    problems.Add(new($"{path}[{index}]", $"Starting unit {slot + 1} would stand on {Name(tile)}, which must be a passable tile in the {half} half."));
                }
            }
        }
    }

    // No tile where one player's starting units could stand is one where another player's could: two players' units never
    // share a tile, whichever candidates are drawn.
    private static void CheckSharedRows(Ruleset ruleset, GameMap map, ImmutableArray<ImmutableArray<HexCoord>> spawns, List<RulesetProblem> problems)
    {
        var owners = new Dictionary<HexCoord, int>();
        for (var group = 0; group < spawns.Length; group++)
        {
            var tiles = spawns[group].SelectMany(spawn => SpawnPlanner.Row(ruleset, map, spawn)).Distinct().ToList();
            if (tiles.Any(tile => owners.TryGetValue(tile, out var other) && other != group))
            {
                problems.Add(new($"$.spawns[{group}]", "A starting unit could stand on a tile where another player's starting units can stand."));
            }

            foreach (var tile in tiles)
            {
                owners.TryAdd(tile, group);
            }
        }
    }

    // On a two-player map the first player starts in the left half and the second in the right, as on a generated map.
    private static void CheckOppositeHalves(GameMap map, ImmutableArray<ImmutableArray<HexCoord>> spawns, List<RulesetProblem> problems)
    {
        for (var group = 0; group < spawns.Length; group++)
        {
            for (var index = 0; index < spawns[group].Length; index++)
            {
                if (group == 0 ? !IsLeft(map, spawns[group][index]) : !IsRight(map, spawns[group][index]))
                {
                    problems.Add(new($"$.spawns[{group}][{index}]", $"On a map for two players, player {group + 1}'s spawn must lie in the {(group == 0 ? "left" : "right")} half."));
                }
            }
        }
    }

    // Each resource's yield in the left and the right half (a river edge counts in the half of the tile that owns it; the middle
    // column of an odd width in neither).
    private static void CheckHalves(Ruleset ruleset, GameMap map, List<RulesetProblem> problems)
    {
        var left = new Dictionary<ResourceType, int>();
        var right = new Dictionary<ResourceType, int>();
        foreach (var tile in map.Tiles)
        {
            Add(HalfOf(map, tile.Coord, left, right), ruleset.Terrain(tile.Terrain).Yields);
        }

        foreach (var edge in Edges(map))
        {
            if (map.RiverBetween(edge.Owner, edge.Neighbor()) is { } river)
            {
                Add(HalfOf(map, edge.Owner, left, right), ruleset.River(river).Yields);
            }
        }

        foreach (var resource in left.Keys.Union(right.Keys).OrderBy(static resource => resource.Id, StringComparer.Ordinal))
        {
            var (a, b) = (left.GetValueOrDefault(resource), right.GetValueOrDefault(resource));
            if (!Close(a, b, HalfTolerancePercent))
            {
                problems.Add(new("$.rows", $"The left half yields {a} {resource.Id} and the right half {b}: they must be within {HalfTolerancePercent}% of each other."));
            }
        }
    }

    // Every candidate within StartTolerancePercent of the mean of all; every group's average within SideTolerancePercent of the
    // best group's.
    private static void CheckStarts(Ruleset ruleset, GameMap map, ImmutableArray<ImmutableArray<HexCoord>> spawns, List<RulesetProblem> problems)
    {
        var values = spawns.Select(group => group.Select(spawn => StartValue(ruleset, map, spawn)).ToList()).ToList();
        var mean = values.SelectMany(static group => group).Average();
        for (var group = 0; group < spawns.Length; group++)
        {
            for (var index = 0; index < spawns[group].Length; index++)
            {
                if (Math.Abs(values[group][index] - mean) * 100 > mean * StartTolerancePercent)
                {
                    problems.Add(new($"$.spawns[{group}][{index}]", $"The start at {Name(spawns[group][index])} is worth {values[group][index]}, more than {StartTolerancePercent}% from the mean of all starts ({mean:0.#}): the yields within {StartRadius} tiles must be about the same for every spawn."));
                }
            }
        }

        var means = values.Select(static group => group.Average()).ToList();
        var best = means.Max();
        for (var group = 0; group < means.Count; group++)
        {
            if ((best - means[group]) * 100 > best * SideTolerancePercent)
            {
                problems.Add(new($"$.spawns[{group}]", $"Player {group + 1}'s starts are worth {means[group]:0.#} on average and the best player's {best:0.#}: they must be within {SideTolerancePercent}% of each other."));
            }
        }
    }

    // Every edge between two tiles of the map once, by the tile that owns it.
    private static IEnumerable<HexEdge> Edges(GameMap map) => map.Tiles
        .SelectMany(static tile => Enum.GetValues<HexSide>().Select(side => new HexEdge(tile.Coord, side)))
        .Where(edge => map.Contains(edge.Neighbor()));

    private static bool IsLeft(GameMap map, HexCoord coord) => coord.Column < map.Width / 2;

    private static bool IsRight(GameMap map, HexCoord coord) => coord.Column >= map.Width - (map.Width / 2);

    private static Dictionary<ResourceType, int>? HalfOf(GameMap map, HexCoord coord, Dictionary<ResourceType, int> left, Dictionary<ResourceType, int> right) =>
        IsLeft(map, coord) ? left : IsRight(map, coord) ? right : null;

    private static void Add(Dictionary<ResourceType, int>? totals, ImmutableArray<ResourceAmount> yields)
    {
        if (totals is null)
        {
            return;
        }

        foreach (var amount in yields)
        {
            totals[amount.Resource] = totals.GetValueOrDefault(amount.Resource) + amount.Amount;
        }
    }

    private static int Total(ImmutableArray<ResourceAmount> yields) => yields.Sum(static amount => amount.Amount);

    private static bool Close(int a, int b, int percent) => Math.Abs(a - b) * 100 <= Math.Max(a, b) * percent;

    private static string Name(HexCoord coord) => $"[{coord.Column}, {coord.Row}]";
}
