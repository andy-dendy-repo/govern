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
            foreach (var next in current.Neighbors().Where(next => map.IsPassable(TestRulesets.Default, next) && reached.Add(next)))
            {
                pending.Push(next);
            }
        }

        return reached;
    }
}
