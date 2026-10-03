using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class CloseTradeRouteActionTests
{
    [Fact]
    public void Execute_Owner_RemovesTheRouteAndFreesTheTrader()
    {
        var scenario = new TradeScenario(hostOwner: P1);
        var (state, route) = scenario.Proposed();

        var update = Execute(state, P1, new CloseTradeRouteCommand(route));

        update.State.TradeRouteList.ShouldBeEmpty();
        var trader = update.State.FindUnit(scenario.Trader)!;
        (trader.RouteId, trader.Destination).ShouldBe((null, null));
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Change.ShouldBe(TradeChange.Closed);
        Execute(update.State, P1, new MoveUnitCommand(scenario.Trader, At(5, 2)));
    }

    [Fact]
    public void Execute_BeforeTheTraderArrives_IsHiddenFromTheHost()
    {
        var scenario = new TradeScenario();
        var sent = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;
        var route = sent.TradeRouteList.ShouldHaveSingleItem().Id;

        var update = Execute(sent, P1, new CloseTradeRouteCommand(route));

        VisibilityCalculator.EventsFor(TestRulesets.Default, sent, update, P1).ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Unopened.ShouldBeTrue();
        VisibilityCalculator.EventsFor(TestRulesets.Default, sent, update, P2).ShouldBeEmpty();
    }

    [Fact]
    public void Execute_OpenRoute_IsSeenByTheHost()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Opened();

        var update = Execute(state, P1, new CloseTradeRouteCommand(route));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P2).ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Change.ShouldBe(TradeChange.Closed);
    }

    [Fact]
    public void Execute_Host_IsNotFound()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Accepted();

        GameEngine.Execute(TestRulesets.Default, state, P2, new CloseTradeRouteCommand(route)).ShouldFailWith(RuleViolations.RouteNotFound);
    }
}
