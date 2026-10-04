using Govern.Core.Map;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.State;

public class GameFactoryPredefinedMapTests
{
    private static readonly PredefinedMap Basin = PredefinedMap.ParseValid(
        TestRulesets.Default, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "maps", "basin.json")));

    private static GameState Create(ulong seed) =>
        GameFactory.Create(TestRulesets.Default, new GameSetup([GameStateBuilder.P1, GameStateBuilder.P2], seed, Map: Basin));

    [Fact]
    public void Create_WithAMap_PlaysOnItsTilesUnchanged()
    {
        var state = Create(seed: 3);

        state.Map.Width.ShouldBe(Basin.Map.Width);
        state.Map.Height.ShouldBe(Basin.Map.Height);
        state.Map.Tiles.Select(static tile => (tile.Terrain, tile.Rivers)).ShouldBe(Basin.Map.Tiles.Select(static tile => (tile.Terrain, tile.Rivers)));
    }

    [Fact]
    public void Create_WithAMapAndASize_TheMapsSizeWins()
    {
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([GameStateBuilder.P1, GameStateBuilder.P2], 3, Width: 10, Height: 10, Map: Basin));

        state.Map.Width.ShouldBe(Basin.Map.Width);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(17UL)]
    [InlineData(123UL)]
    public void Create_WithAMap_PutsEachPlayerOnOneOfTheirCandidates(ulong seed)
    {
        var state = Create(seed);
        var first = state.Units.Where(static unit => unit.Owner == GameStateBuilder.P1).Select(static unit => unit.Position).ToList();
        var second = state.Units.Where(static unit => unit.Owner == GameStateBuilder.P2).Select(static unit => unit.Position).ToList();

        Basin.FirstSpawns.ShouldContain(first[0]);
        Basin.SecondSpawns.ShouldContain(second[0]);
        first[1].ShouldBe(HexCoord.FromOffset(first[0].Column + 1, first[0].Row));
        second[1].ShouldBe(HexCoord.FromOffset(second[0].Column - 1, second[0].Row));
    }

    [Fact]
    public void Create_WithAMap_ManySeedsUseEveryCandidateForBothPlayersIndependently()
    {
        var pairs = Enumerable.Range(1, 60)
            .Select(static seed => Create((ulong)seed))
            .Select(static state => (First: Basin.FirstSpawns.IndexOf(state.Units[0].Position), Second: Basin.SecondSpawns.IndexOf(state.Units[2].Position)))
            .ToHashSet();

        pairs.Select(static pair => pair.First).Distinct().Count().ShouldBe(Basin.FirstSpawns.Length);
        pairs.Select(static pair => pair.Second).Distinct().Count().ShouldBe(Basin.SecondSpawns.Length);
        pairs.ShouldContain(static pair => pair.First != pair.Second);
    }

    [Fact]
    public void Create_WithAMap_SameSeedGivesTheSameState()
    {
        GameSerializer.Serialize(Create(9)).ShouldBe(GameSerializer.Serialize(Create(9)));
    }

    [Fact]
    public void Create_WithAMapTooLargeForTheRuleset_Throws()
    {
        var large = Basin with { Map = GameMap.Create(41, 20, TestRulesets.Plains) };

        Should.Throw<ArgumentOutOfRangeException>(() =>
            GameFactory.Create(TestRulesets.Default, new GameSetup([GameStateBuilder.P1, GameStateBuilder.P2], 1, Map: large)));
    }

    [Fact]
    public void Create_WithAMapOfAnUnknownTerrain_Throws()
    {
        var unknown = Basin with { Map = GameMap.Create(20, 20, new TerrainType("swamp")) };

        Should.Throw<ArgumentException>(() =>
            GameFactory.Create(TestRulesets.Default, new GameSetup([GameStateBuilder.P1, GameStateBuilder.P2], 1, Map: unknown)));
    }
}
