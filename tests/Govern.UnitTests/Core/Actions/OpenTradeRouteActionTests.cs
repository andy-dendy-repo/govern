using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class OpenTradeRouteActionTests
{
    [Fact]
    public void Execute_TraderSent_WalksAtOnceAndOpensTheRouteWhenItReachesThePost()
    {
        var scenario = new TradeScenario();

        var sent = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host));
        var afterP1 = Execute(sent.State, P1, new EndTurnCommand());
        var arrived = Execute(afterP1.State, P2, new EndTurnCommand());

        var route = sent.State.TradeRouteList.ShouldHaveSingleItem();
        (route.Owner, route.Home, route.Host, route.HostOwner, route.Trader, route.Opened).ShouldBe((P1, scenario.Home, scenario.Host, P2, scenario.Trader, false));
        var walking = sent.State.FindUnit(scenario.Trader)!;
        (walking.Position, walking.RouteId, walking.Destination).ShouldBe((At(5, 2), route.Id, At(7, 2)));
        sent.Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>();
        arrived.State.FindRoute(route.Id)!.Opened.ShouldBeTrue();
        arrived.State.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(6, 2)),
            static trader => trader.Destination.ShouldBeNull());
        arrived.Events.OfType<TradeRouteChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Opened);
    }

    [Fact]
    public void Execute_UnitThatCantTrade_IsRejected()
    {
        var scenario = new TradeScenario();
        var soldier = scenario.Builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new OpenTradeRouteCommand(soldier, scenario.Home, scenario.Host))
            .ShouldFailWith(RuleViolations.CannotTrade);
    }

    [Fact]
    public void Execute_HomeWithoutATradePost_IsRejected()
    {
        var scenario = new TradeScenario();
        var other = scenario.Builder.AddCity(P1, 3, 6);

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, other, scenario.Host))
            .ShouldFailWith(RuleViolations.NoTradePost);
    }

    [Fact]
    public void Execute_EnemyHostOutOfSight_HasNoPartner()
    {
        var scenario = new TradeScenario();
        var state = scenario.Builder.Build();
        var blind = state with { Units = [.. state.Units.Where(static unit => unit.Type != TestRulesets.Scout)] };

        GameEngine.Execute(TestRulesets.Default, blind, P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host))
            .ShouldFailWith(RuleViolations.NoTradePartner);
        GameEngine.Execute(TestRulesets.Default, blind, P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Home))
            .ShouldFailWith(RuleViolations.NoTradePartner);
    }

    [Fact]
    public void Execute_ThirdRouteFromACity_IsRejected()
    {
        var scenario = new TradeScenario();
        var second = scenario.Builder.AddUnit(P1, TestRulesets.Trader, 2, 3);
        var third = scenario.Builder.AddUnit(P1, TestRulesets.Trader, 1, 2);
        var near = scenario.Builder.AddCity(P1, 2, 6, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var far = scenario.Builder.AddCity(P1, 6, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (2, 5)).WithBuilding(TestRulesets.TradePost, (6, 6));
        var state = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;
        state = Execute(state, P1, new OpenTradeRouteCommand(second, scenario.Home, near)).State;

        GameEngine.Execute(TestRulesets.Default, state, P1, new OpenTradeRouteCommand(third, scenario.Home, far)).ShouldFailWith(RuleViolations.TooManyRoutes);
        GameEngine.Execute(TestRulesets.Default, state, P1, new OpenTradeRouteCommand(third, scenario.Home, near)).ShouldFailWith(RuleViolations.RouteHasTrader);
        GameEngine.Execute(TestRulesets.Default, state, P1, new OpenTradeRouteCommand(third, far, scenario.Home)).ShouldSucceed();
    }

    [Fact]
    public void Execute_TraderOnARoute_TakesNoOtherOrders()
    {
        var scenario = new TradeScenario();
        var state = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;
        var p1Turn = EndRound(state);

        GameEngine.Execute(TestRulesets.Default, p1Turn, P1, new MoveUnitCommand(scenario.Trader, At(6, 3))).ShouldFailWith(RuleViolations.OnTradeRoute);
        GameEngine.Execute(TestRulesets.Default, p1Turn, P1, new SetDestinationCommand(scenario.Trader, At(2, 4))).ShouldFailWith(RuleViolations.OnTradeRoute);
        GameEngine.Execute(TestRulesets.Default, p1Turn, P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host))
            .ShouldFailWith(RuleViolations.OnTradeRoute);
    }

    [Fact]
    public void Execute_RouteThatLostItsTrader_IsServedByANewOne()
    {
        var scenario = new TradeScenario();
        var spare = scenario.Builder.AddUnit(P1, TestRulesets.Trader, 2, 3);
        var (state, route) = scenario.Opened();
        var lost = Execute(state with { Units = [.. state.Units.Where(unit => unit.Id != scenario.Trader)] }, P1, new SetMarketOrderCommand(scenario.Home, TestRulesets.Gold, null)).State;

        var update = Execute(lost, P1, new OpenTradeRouteCommand(spare, scenario.Home, scenario.Host));

        update.State.TradeRouteList.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            served => served.Id.ShouldBe(route),
            served => served.Trader.ShouldBe(spare),
            static served => served.Opened.ShouldBeTrue());
    }
}
