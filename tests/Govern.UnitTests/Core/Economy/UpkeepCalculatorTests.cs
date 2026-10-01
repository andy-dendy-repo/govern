using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.Economy;

public class UpkeepCalculatorTests
{
    [Fact]
    public void Missing_PartlyCovered_ListsOnlyWhatIsShortInRulesetOrder()
    {
        var missing = UpkeepCalculator.Missing(
            TestRulesets.Default,
            [new(TestRulesets.Water, 1), new(TestRulesets.Food, 5)],
            [new(TestRulesets.Water, 3), new(TestRulesets.Food, 2), new(TestRulesets.Stone, 1)]);

        missing.ShouldBe([new(TestRulesets.Water, 2), new(TestRulesets.Stone, 1)]);
    }

    [Fact]
    public void Subtract_DropsResourcesThatReachZero()
    {
        UpkeepCalculator.Subtract(TestRulesets.Default, [new(TestRulesets.Food, 2), new(TestRulesets.Wood, 3)], [new(TestRulesets.Food, 2)])
            .ShouldBe([new ResourceAmount(TestRulesets.Wood, 3)]);
    }

    [Fact]
    public void Pay_EquallyHurtUnits_PaysTheLowerIdFirst()
    {
        UnitId first = new(4), second = new(9);

        var result = UpkeepCalculator.Pay(
            TestRulesets.Default,
            [new(TestRulesets.Food, 1), new(TestRulesets.Water, 1)],
            [],
            [new UpkeepUnit(second, TestRulesets.Soldier, 0), new UpkeepUnit(first, TestRulesets.Soldier, 0)],
            UpkeepPriority.BuildingsFirst);

        result.UnpaidUnits.ShouldBe([second]);
        result.Paid.ShouldBe([new(TestRulesets.Food, 1), new(TestRulesets.Water, 1)]);
        result.Stock.ShouldBeEmpty();
    }

    [Fact]
    public void Pay_BuildingsWithoutUpkeep_AreAlwaysPaid()
    {
        var result = UpkeepCalculator.Pay(TestRulesets.Default, [], [TestRulesets.Harbour, TestRulesets.Campus], [], UpkeepPriority.BuildingsFirst);

        result.UnpaidBuildings.ShouldBe([TestRulesets.Campus]);
    }
}
