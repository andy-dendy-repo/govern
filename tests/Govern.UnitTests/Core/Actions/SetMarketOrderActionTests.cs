using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class SetMarketOrderActionTests
{
    [Fact]
    public void Execute_NewOrder_IsListedInTheRulesetsResourceOrder()
    {
        var scenario = new TradeScenario();

        var update = Execute(scenario.Builder.Build(), P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Wood, MarketSide.Sell, 5));

        update.State.FindCity(scenario.Home)!.MarketList
            .ShouldBe([Order(TestRulesets.Food, MarketSide.Sell, 6), Order(TestRulesets.Wood, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 4)]);
        update.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_NoSide_RemovesTheOrder()
    {
        var scenario = new TradeScenario();

        var update = Execute(scenario.Builder.Build(), P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Food, null));

        update.State.FindCity(scenario.Home)!.MarketList.ShouldBe([Order(TestRulesets.Stone, MarketSide.Buy, 4)]);
    }

    [Theory]
    [InlineData("water", 3)]
    [InlineData("culture", 3)]
    [InlineData("iron", 3)]
    public void Execute_ResourceThatCantBeTraded_IsRejected(string resource, int cap)
    {
        var scenario = new TradeScenario();

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new SetMarketOrderCommand(scenario.Home, new ResourceType(resource), MarketSide.Sell, cap))
            .ShouldFailWith(RuleViolations.NotTradable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Execute_CapOutOfRange_IsRejected(int cap)
    {
        var scenario = new TradeScenario();

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Gold, MarketSide.Buy, cap))
            .ShouldFailWith(RuleViolations.CapOutOfRange);
    }

    [Fact]
    public void Execute_EnemyCity_IsNotFound()
    {
        var scenario = new TradeScenario();

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new SetMarketOrderCommand(scenario.Host, TestRulesets.Gold, MarketSide.Buy, 2))
            .ShouldFailWith(RuleViolations.CityNotFound);
    }

    [Fact]
    public void Execute_CapBelowWhatAnAgreedDealUses_IsRejectedUntilTheDealEnds()
    {
        var scenario = new TradeScenario();
        var (accepted, route) = scenario.Accepted();
        var state = Execute(accepted, P2, new EndTurnCommand()).State;

        var below = GameEngine.Execute(TestRulesets.Default, state, P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Food, MarketSide.Sell, 2));
        var flipped = GameEngine.Execute(TestRulesets.Default, state, P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Food, MarketSide.Buy, 6));
        var cancelled = Execute(state, P1, new CancelDealCommand(route)).State;

        below.ShouldFailWith(RuleViolations.CapBelowDeals);
        flipped.ShouldFailWith(RuleViolations.CapBelowDeals);
        Execute(state, P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Food, MarketSide.Sell, 3));
        Execute(cancelled, P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Food, null));
    }
}
