using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Economy;

public class TradeCalculatorTests
{
    [Theory]
    [InlineData(100, 4, 0, 108)]
    [InlineData(100, 0, 6, 90)]
    [InlineData(100, 0, 0, 100)]
    [InlineData(390, 1000, 0, 400)]
    [InlineData(26, 0, 1000, 25)]
    [InlineData(200, 5, 5, 200)]
    public void NextDemand_NeedAndProposition_MoveTheLevelWithinItsBounds(int percent, int need, int proposition, int expected)
    {
        TradeCalculator.NextDemand(TestRulesets.Default.Trade, percent, need, proposition).ShouldBe(expected);
    }

    [Fact]
    public void Value_TradeValueTimesDemand()
    {
        ResourceDemand[] demand = [new(TestRulesets.Stone, 150)];

        TradeCalculator.Value(TestRulesets.Default, demand, TestRulesets.Stone).ShouldBe(300);
        TradeCalculator.Value(TestRulesets.Default, demand, TestRulesets.Gold).ShouldBe(300);
        TradeCalculator.Value(TestRulesets.Default, demand, TestRulesets.Water).ShouldBe(0);
    }

    [Fact]
    public void Committed_ProposalsCountForTheirHomeCityOnly()
    {
        var (home, host) = (new CityId(1), new CityId(2));
        var proposal = new TradeDeal(Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1)));
        (CityId, CityId, TradeDeal?)[] routes = [(home, host, proposal), (home, host, proposal with { Accepted = true })];

        TradeCalculator.Committed(routes, home, TestRulesets.Food, MarketSide.Sell).ShouldBe(6);
        TradeCalculator.Committed(routes, home, TestRulesets.Stone, MarketSide.Buy).ShouldBe(2);
        TradeCalculator.Committed(routes, host, TestRulesets.Food, MarketSide.Buy).ShouldBe(3);
        TradeCalculator.Committed(routes, host, TestRulesets.Stone, MarketSide.Sell).ShouldBe(1);
        TradeCalculator.FreeMarket([Order(TestRulesets.Stone, MarketSide.Sell, 4)], routes, host).ShouldBe([Order(TestRulesets.Stone, MarketSide.Sell, 3)]);
    }

    [Fact]
    public void CheckDeal_NullItem_IsABadAmount()
    {
        MarketOrder[] home = [Order(TestRulesets.Food, MarketSide.Sell, 20), Order(TestRulesets.Stone, MarketSide.Buy, 20)];
        MarketOrder[] host = [Order(TestRulesets.Food, MarketSide.Buy, 20), Order(TestRulesets.Stone, MarketSide.Sell, 20)];

        TradeCalculator.CheckDeal(TestRulesets.Default, home, host, [null!], Amounts((TestRulesets.Stone, 1))).ShouldBe(DealProblem.BadAmount);
        TradeCalculator.CheckDeal(TestRulesets.Default, home, host, Amounts((TestRulesets.Food, 1)), [null!]).ShouldBe(DealProblem.BadAmount);
    }

    [Fact]
    public void NextDemand_HugeNeed_DoesNotOverflow()
    {
        TradeCalculator.NextDemand(TestRulesets.Default.Trade, 400, int.MaxValue / 2, 0).ShouldBe(400);
    }

    [Fact]
    public void CheckDeal_SameResourceTwiceOnASide_IsABadAmount()
    {
        MarketOrder[] wide = [Order(TestRulesets.Food, MarketSide.Sell, 20), Order(TestRulesets.Stone, MarketSide.Buy, 20)];
        MarketOrder[] host = [Order(TestRulesets.Food, MarketSide.Buy, 20), Order(TestRulesets.Stone, MarketSide.Sell, 20)];

        TradeCalculator.CheckDeal(TestRulesets.Default, wide, host, Amounts((TestRulesets.Food, 1), (TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1)))
            .ShouldBe(DealProblem.BadAmount);
        TradeCalculator.CheckDeal(TestRulesets.Default, wide, host, Amounts((TestRulesets.Food, 2)), Amounts((TestRulesets.Stone, 1))).ShouldBeNull();
    }
}
