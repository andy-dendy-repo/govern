using Govern.Core.Economy;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Economy;

public class YieldCalculatorTests
{
    [Fact]
    public void TileYields_Terrain_GivesItsOwnYields()
    {
        YieldCalculator.TileYields(TestRulesets.Default, TestRulesets.Forest, [], improvement: null)
            .ShouldBe([new(TestRulesets.Food, 1), new(TestRulesets.Water, 1), new(TestRulesets.Wood, 2)]);
    }

    [Fact]
    public void TileYields_Desert_GivesNothing()
    {
        YieldCalculator.TileYields(TestRulesets.Default, TestRulesets.Desert, [], improvement: null).ShouldBeEmpty();
    }

    [Fact]
    public void TileYields_SeveralRivers_AddTheMostAnyOfThemGivesNotTheSum()
    {
        YieldCalculator.TileYields(TestRulesets.Default, TestRulesets.Desert, [TestRulesets.Stream, TestRulesets.River, TestRulesets.River], improvement: null)
            .ShouldBe([new(TestRulesets.Water, 1)]);
    }

    [Fact]
    public void TileYields_TerrainRiverAndImprovement_AddUpInRulesetOrder()
    {
        YieldCalculator.TileYields(TestRulesets.Default, TestRulesets.Plains, [TestRulesets.DeepRiver], TestRulesets.Farm)
            .ShouldBe([new(TestRulesets.Food, 4), new(TestRulesets.Water, 2)]);
    }

    [Fact]
    public void Sum_MixedAmounts_AddsPerResourceInRulesetOrderAndDropsZeros()
    {
        YieldCalculator.Sum(TestRulesets.Default, [new(TestRulesets.Culture, 2), new(TestRulesets.Food, 1), new(TestRulesets.Culture, -2), new(TestRulesets.Food, 3)])
            .ShouldBe([new(TestRulesets.Food, 4)]);
    }

    [Fact]
    public void CityIncome_AddsTheCitysOwnYieldsAndItsBuildings()
    {
        YieldCalculator.CityIncome(TestRulesets.Default, [new(TestRulesets.Food, 2)], [TestRulesets.Campus])
            .ShouldBe([new(TestRulesets.Food, 2), new(TestRulesets.Science, 3), new(TestRulesets.Culture, 1)]);
    }

    [Fact]
    public void AmountOf_MissingResource_IsZero()
    {
        YieldCalculator.AmountOf([new(TestRulesets.Food, 2)], TestRulesets.Gold).ShouldBe(0);
    }
}
