using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class CancelDealActionTests
{
    [Fact]
    public void Execute_HostCancels_EndsTheDealAndTheTraderReturnsToThePost()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Accepted();

        var update = Execute(state, P2, new CancelDealCommand(route));

        var route2 = update.State.FindRoute(route)!;
        route2.Deal.ShouldBeNull();
        route2.Trader.ShouldBe(scenario.Trader);
        update.State.FindUnit(scenario.Trader)!.Destination.ShouldBeNull();
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Change.ShouldBe(TradeChange.Cancelled);
    }

    [Fact]
    public void Execute_OwnerCancelsAwayFromThePost_SendsTheTraderBackToIt()
    {
        var scenario = new TradeScenario();
        var (accepted, route) = scenario.Accepted();
        var walkedHome = Execute(accepted, P2, new EndTurnCommand()).State;

        var update = Execute(walkedHome, P1, new CancelDealCommand(route));

        walkedHome.FindUnit(scenario.Trader)!.Position.DistanceTo(At(7, 2)).ShouldBeGreaterThan(1);
        update.State.FindUnit(scenario.Trader)!.Destination.ShouldBe(At(7, 2));
    }

    [Fact]
    public void Execute_NoDeal_IsRejected()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Opened();

        GameEngine.Execute(TestRulesets.Default, state, P1, new CancelDealCommand(route)).ShouldFailWith(RuleViolations.NoDeal);
        GameEngine.Execute(TestRulesets.Default, state, P1, new CancelDealCommand(new TradeRouteId(12345))).ShouldFailWith(RuleViolations.RouteNotFound);
    }
}
