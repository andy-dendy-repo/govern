using System.Text.Json.Nodes;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Map;

// The generator is internal; it is tested through the games GameFactory creates with it.
public class MapGeneratorTests
{
    public static TheoryData<ulong, int, int> Maps => new()
    {
        { 1UL, 20, 14 },
        { 7UL, 20, 14 },
        { 42UL, 8, 8 },
        { 99UL, 40, 40 },
        { 5UL, 13, 9 },
    };

    [Theory]
    [MemberData(nameof(Maps))]
    public void Create_AnySize_BothHalvesAreTheSame(ulong seed, int width, int height)
    {
        var map = Create(seed, width, height).Map;

        map.Tiles.ShouldAllBe(tile => map.TileAt(map.MirrorOf(tile.Coord)).Terrain == tile.Terrain);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Create_AnySize_StartingUnitsStandOnTheDefaultTerrain(ulong seed, int width, int height)
    {
        var state = Create(seed, width, height);

        state.Units.ShouldAllBe(unit => state.Map.TileAt(unit.Position).Terrain == TestRulesets.Plains);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Create_AnySize_EveryPassableTileIsReachableFromEveryUnit(ulong seed, int width, int height)
    {
        var state = Create(seed, width, height);

        var passable = state.Map.Tiles.Select(static tile => tile.Coord).Where(coord => state.Map.IsPassable(TestRulesets.Default, coord)).ToHashSet();
        foreach (var unit in state.Units)
        {
            Flood(state.Map, unit.Position).ShouldBe(passable, ignoreOrder: true);
        }
    }

    [Theory]
    [InlineData(3UL, 20, 14)]
    [InlineData(8UL, 40, 40)]
    public void Create_RoomyMap_CoversRoughlyEachTerrainsShare(ulong seed, int width, int height)
    {
        var map = Create(seed, width, height).Map;

        foreach (var patch in TestRulesets.Default.Map.Generation.Patches)
        {
            var target = map.Tiles.Length * patch.SharePerMille / 1000;
            map.Tiles.Count(tile => tile.Terrain == patch.Terrain).ShouldBeInRange(target - 4, target + 1, patch.Terrain.Id);
        }
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Create_AnySize_RiversAreMirrored(ulong seed, int width, int height)
    {
        var map = Create(seed, width, height).Map;

        Rivers(map).ShouldAllBe(edge => map.RiverBetween(map.MirrorOf(edge.A), map.MirrorOf(edge.B)) == edge.River);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Create_AnySize_EveryRiverEdgeTouchesAPassableTile(ulong seed, int width, int height)
    {
        var map = Create(seed, width, height).Map;

        Rivers(map).ShouldAllBe(edge => map.IsPassable(TestRulesets.Default, edge.A) || map.IsPassable(TestRulesets.Default, edge.B));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(12UL)]
    public void Create_DefaultMap_RiversRunFromHillsOrMountainsToALakeOrTheBorder(ulong seed)
    {
        var map = Create(seed, 20, 14).Map;
        var rules = TestRulesets.Default.Map.Generation.Rivers;
        var systems = RiverSystems(map);

        systems.ShouldNotBeEmpty();
        foreach (var system in systems)
        {
            system.ShouldContain(edge => Corners(edge.A, edge.B).Any(corner => corner.Any(coord => map.Contains(coord) && rules.Sources.Contains(map.TileAt(coord).Terrain))));
            system.ShouldContain(edge => Corners(edge.A, edge.B).Any(corner => corner.Any(coord => !map.Contains(coord) || rules.Mouths.Contains(map.TileAt(coord).Terrain))));
            system.Count.ShouldBeGreaterThanOrEqualTo(rules.MinLength);
        }
    }

    [Fact]
    public void Create_DefaultMaps_HaveEveryRiverKind()
    {
        var kinds = Enumerable.Range(1, 12).SelectMany(seed => Rivers(Create((ulong)seed, 20, 14).Map)).Select(static edge => edge.River).Distinct();

        kinds.ShouldBe([TestRulesets.Stream, TestRulesets.River, TestRulesets.DeepRiver], ignoreOrder: true);
    }

    [Fact]
    public void Create_NoRivers_LeavesEveryTileWithoutRivers()
    {
        var ruleset = TestRulesets.With(json => json["map"]!["generation"]!["rivers"]!["count"] = 0);

        GameFactory.Create(ruleset, new GameSetup([P1, P2], Seed: 1)).Map.Tiles.ShouldAllBe(static tile => tile.Rivers == null);
    }

    [Fact]
    public void Create_DeepRiversEverywhere_KeepTheMapConnected()
    {
        // Many long rivers, deep from their second edge: the generator must still leave every passable tile reachable.
        var ruleset = TestRulesets.With(json =>
        {
            var rivers = json["map"]!["generation"]!["rivers"]!;
            rivers["count"] = 20;
            rivers["maxLength"] = 100;
            rivers["stages"] = new JsonArray(
                new JsonObject { ["river"] = "stream", ["fromSegment"] = 0 },
                new JsonObject { ["river"] = "deep_river", ["fromSegment"] = 1 });
        });

        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var state = GameFactory.Create(ruleset, new GameSetup([P1, P2], seed));
            var passable = state.Map.Tiles.Select(static tile => tile.Coord).Where(coord => state.Map.IsPassable(ruleset, coord)).ToHashSet();

            Rivers(state.Map).Count(edge => edge.River == TestRulesets.DeepRiver).ShouldBeGreaterThan(10);
            Flood(state.Map, state.Units[0].Position).ShouldBe(passable, ignoreOrder: true);
        }
    }

    [Fact]
    public void Create_DifferentSeeds_GiveDifferentMaps()
    {
        var maps = Enumerable.Range(1, 5)
            .Select(seed => string.Join(',', Create((ulong)seed, 20, 14).Map.Tiles.Select(static tile => tile.Terrain.Id)))
            .Distinct();

        maps.Count().ShouldBe(5);
    }

    [Fact]
    public void Create_TargetOutOfReach_StopsAfterABoundedNumberOfSeeds()
    {
        // Patches of one tile on an 8×8 map: the lake and mountain shares compete for the few tiles that keep the map connected.
        var ruleset = TestRulesets.With(json => json["map"]!["generation"]!["patches"] = new JsonArray(
            new JsonObject { ["terrain"] = "forest", ["sharePerMille"] = 350, ["patchSize"] = 1 },
            new JsonObject { ["terrain"] = "lake", ["sharePerMille"] = 150, ["patchSize"] = 1 }));

        var state = GameFactory.Create(ruleset, new GameSetup([P1, P2], Seed: 4, Width: 8, Height: 8));

        state.Map.Tiles.Count(tile => tile.Terrain == TestRulesets.Forest).ShouldBeLessThanOrEqualTo(64 * 350 / 1000);
        state.Units.ShouldAllBe(unit => state.Map.TileAt(unit.Position).Terrain == TestRulesets.Plains);
    }

    [Fact]
    public void Create_ImpassableWall_KeepsTheMapConnected()
    {
        // Only lake, as much as the validator allows, in long patches: the generator must still leave one connected land.
        var ruleset = TestRulesets.With(json => json["map"]!["generation"]!["patches"] = new JsonArray(
            new JsonObject { ["terrain"] = "lake", ["sharePerMille"] = 150, ["patchSize"] = 40 }));

        var state = GameFactory.Create(ruleset, new GameSetup([P1, P2], Seed: 11));

        var passable = state.Map.Tiles.Select(static tile => tile.Coord).Where(coord => state.Map.IsPassable(ruleset, coord)).ToHashSet();
        Flood(state.Map, state.Units[0].Position).ShouldBe(passable, ignoreOrder: true);
        state.Map.Tiles.Count(tile => tile.Terrain == TestRulesets.Lake).ShouldBeGreaterThan(20);
    }

    private static GameState Create(ulong seed, int width, int height) =>
        GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], seed, width, height));

    private static HashSet<HexCoord> Flood(GameMap map, HexCoord start)
    {
        var reached = new HashSet<HexCoord> { start };
        var pending = new Stack<HexCoord>([start]);
        while (pending.TryPop(out var current))
        {
            foreach (var next in current.Neighbors().Where(next => map.IsPassable(TestRulesets.Default, next) && CanCross(map, current, next) && reached.Add(next)))
            {
                pending.Push(next);
            }
        }

        return reached;
    }

    private static bool CanCross(GameMap map, HexCoord from, HexCoord to) =>
        map.RiverBetween(from, to) is not { } river || TestRulesets.Default.River(river).IsCrossable;

    // Every river edge of a map, with its two tiles and kind.
    private static List<(HexCoord A, HexCoord B, RiverType River)> Rivers(GameMap map) =>
        [.. map.Tiles.SelectMany(tile => Enum.GetValues<HexSide>()
            .Where(side => tile.Rivers?.On(side) is not null)
            .Select(side => (tile.Coord, new HexEdge(tile.Coord, side).Neighbor(), tile.Rivers!.On(side)!.Value)))];

    // Groups river edges that touch at a corner (two edges of a common tile whose other tiles are neighbours).
    private static List<List<(HexCoord A, HexCoord B, RiverType River)>> RiverSystems(GameMap map)
    {
        var edges = Rivers(map);
        var systems = new List<List<(HexCoord A, HexCoord B, RiverType River)>>();
        var left = edges.ToList();
        while (left.Count > 0)
        {
            var system = new List<(HexCoord A, HexCoord B, RiverType River)> { left[0] };
            left.RemoveAt(0);
            for (var index = 0; index < system.Count; index++)
            {
                var touching = left.Where(other => Touch(system[index], other)).ToList();
                system.AddRange(touching);
                left.RemoveAll(touching.Contains);
            }

            systems.Add(system);
        }

        return systems;

        static bool Touch((HexCoord A, HexCoord B, RiverType) x, (HexCoord A, HexCoord B, RiverType) y)
        {
            var shared = new[] { x.A, x.B }.Intersect([y.A, y.B]).ToList();
            if (shared.Count != 1)
            {
                return false;
            }

            var xOther = x.A == shared[0] ? x.B : x.A;
            var yOther = y.A == shared[0] ? y.B : y.A;
            return xOther.DistanceTo(yOther) == 1;
        }
    }

    // The three tiles at each end of an edge (its two tiles plus one tile next to both).
    private static IEnumerable<HexCoord[]> Corners(HexCoord a, HexCoord b) =>
        a.Neighbors().Where(c => c.DistanceTo(b) == 1).Select(c => new[] { a, b, c });
}
