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
    public void Execute_HostCancelsAnActiveDeal_EndsItAndTheTraderWalksHome()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Active();

        var update = Execute(state, P2, new CancelDealCommand(deal));

        update.State.TradeDealList.ShouldBeEmpty();
        update.State.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.DealId.ShouldBeNull(),
            static trader => trader.Destination.ShouldBe(At(3, 2)));
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeDealChangedEvent>().Change.ShouldBe(TradeChange.Cancelled);
    }

    [Fact]
    public void Execute_OwnerWithdrawsAnOffer_TheTraderIsFreeAgain()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();

        var update = Execute(state, P1, new CancelDealCommand(deal));
        var again = GameEngine.Execute(TestRulesets.Default, update.State, P1, new ProposeDealCommand(scenario.Trader, scenario.Host, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))));

        update.State.TradeDealList.ShouldBeEmpty();
        again.ShouldSucceed();
    }

    [Fact]
    public void Execute_HostDeclinesOrWithdrawsAnOffer_LeavesTheTradersOrdersAlone()
    {
        var scenario = new TradeScenario();
        var (proposed, deal) = scenario.Proposed();
        var sent = Execute(proposed, P1, new SetDestinationCommand(scenario.Trader, At(2, 9))).State;
        var p2Turn = Execute(sent, P1, new EndTurnCommand()).State;

        var update = Execute(p2Turn, P2, new CancelDealCommand(deal));

        update.State.FindUnit(scenario.Trader)!.Destination.ShouldBe(At(2, 9));
    }

    [Fact]
    public void Execute_UnknownOrForeignDeal_IsNotFound()
    {
        var scenario = new TradeScenario();
        var bystander = new TradeScenario(hostOwner: P1);
        var (state, deal) = bystander.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p2Turn, P2, new CancelDealCommand(deal)).ShouldFailWith(RuleViolations.DealNotFound);
        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new CancelDealCommand(new TradeDealId(12345))).ShouldFailWith(RuleViolations.DealNotFound);
    }
}
