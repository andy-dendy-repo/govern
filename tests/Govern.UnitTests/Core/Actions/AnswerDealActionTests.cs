using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class AnswerDealActionTests
{
    [Fact]
    public void Execute_Accept_StartsTheDealAndSendsTheTraderHome()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        var update = Execute(p2Turn, P2, new AnswerDealCommand(route, Accept: true));

        var accepted = update.State.FindRoute(route)!;
        accepted.IsActive.ShouldBeTrue();
        accepted.Deal!.AcceptedTurn.ShouldBe(p2Turn.Turn);
        update.State.FindUnit(scenario.Trader)!.Destination.ShouldBe(At(3, 2));
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Change.ShouldBe(TradeChange.Accepted);
    }

    [Fact]
    public void Execute_Decline_DropsTheProposalAndKeepsTheRoute()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        var update = Execute(p2Turn, P2, new AnswerDealCommand(route, Accept: false));

        update.State.FindRoute(route)!.Deal.ShouldBeNull();
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeRouteChangedEvent>().Change.ShouldBe(TradeChange.Declined);
        GameEngine.Execute(TestRulesets.Default, update.State, P2, new AnswerDealCommand(route, Accept: true)).ShouldFailWith(RuleViolations.NoProposal);
    }

    [Fact]
    public void Execute_ProposerAnsweringItsOwnProposal_IsNotFound()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Proposed();

        GameEngine.Execute(TestRulesets.Default, state, P1, new AnswerDealCommand(route, Accept: true)).ShouldFailWith(RuleViolations.RouteNotFound);
    }

    [Fact]
    public void Execute_BetterOfferOverTheCap_IsAcceptedOnlyAfterCancellingAnOlderDeal()
    {
        // The older deal is listed twice: it counts once, as in the client's preview.
        // The host sells 4 Stone: P1's own deal from another city already takes 4, so P1's enemy offer needs that room.
        var scenario = new TradeScenario();
        var p2Trader = scenario.Builder.AddUnit(P2, TestRulesets.Trader, 8, 3);
        var p2Home = scenario.Builder.AddCity(P2, 8, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (8, 6))
            .WithCity(p2Home, static city => city with { Market = [Order(TestRulesets.Gold, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 4)] })
            .WithCity(scenario.Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Gold, MarketSide.Buy, 5), Order(TestRulesets.Stone, MarketSide.Sell, 4)] });
        var (state, route) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;
        var ownRouteSent = Execute(p2Turn, P2, new OpenTradeRouteCommand(p2Trader, p2Home, scenario.Host));
        var own = ownRouteSent.State.TradeRouteList.Single(other => other.Owner == P2).Id;
        var ownDeal = Execute(ownRouteSent.State, P2, new ProposeDealCommand(own, Amounts((TestRulesets.Gold, 2)), Amounts((TestRulesets.Stone, 4)))).State;

        var full = GameEngine.Execute(TestRulesets.Default, ownDeal, P2, new AnswerDealCommand(route, Accept: true));
        var notThatOne = GameEngine.Execute(TestRulesets.Default, ownDeal, P2, new AnswerDealCommand(route, Accept: true, [route]));
        var update = Execute(ownDeal, P2, new AnswerDealCommand(route, Accept: true, [own, own]));

        ownDeal.FindRoute(own)!.IsActive.ShouldBeTrue();
        full.ShouldFailWith(RuleViolations.HostMarketFull);
        notThatOne.ShouldFailWith(RuleViolations.CannotCancelDeal);
        update.State.FindRoute(route)!.IsActive.ShouldBeTrue();
        update.State.FindRoute(own)!.Deal.ShouldBeNull();
        update.Events.OfType<TradeRouteChangedEvent>().Select(static changed => (changed.RouteId, changed.Change))
            .ShouldBe([(own, TradeChange.Cancelled), (route, TradeChange.Accepted)]);
    }
}
