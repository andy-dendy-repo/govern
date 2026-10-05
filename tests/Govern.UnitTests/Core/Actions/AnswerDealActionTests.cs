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
    public void Execute_Accept_SendsTheTraderToTheHostPostOnItsOwnersTurn()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        var update = Execute(p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Accept));
        var p1Turn = Execute(update.State, P2, new EndTurnCommand()).State;

        update.State.FindDeal(deal)!.Status.ShouldBe(TradeDealStatus.Travelling);
        update.State.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            trader => trader.DealId.ShouldBe(deal),
            static trader => trader.Destination.ShouldBe(At(7, 2)),
            static trader => trader.Position.ShouldBe(At(2, 2)));
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeDealChangedEvent>().Change.ShouldBe(TradeChange.Accepted);
        p1Turn.FindUnit(scenario.Trader)!.Position.ShouldBe(At(5, 2));
    }

    [Fact]
    public void Execute_TraderReachesTheHostPost_StartsTheDealAndTurnsHome()
    {
        var scenario = new TradeScenario();
        var (accepted, deal) = scenario.Accepted();
        var round2 = Execute(accepted, P2, new EndTurnCommand()).State;

        var update = Execute(Execute(round2, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());

        update.State.FindDeal(deal)!.ShouldSatisfyAllConditions(
            static active => active.Status.ShouldBe(TradeDealStatus.Active),
            static active => active.ActiveTurn.ShouldBe(3));
        update.State.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(7, 2)),
            static trader => trader.Destination.ShouldBe(At(3, 2)));
        update.Events.OfType<TradeDealChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Started);
    }

    [Fact]
    public void Execute_Decline_DropsTheDealAndFreesTheTrader()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        var update = Execute(p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Decline));

        update.State.TradeDealList.ShouldBeEmpty();
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeDealChangedEvent>().Change.ShouldBe(TradeChange.Declined);
        GameEngine.Execute(TestRulesets.Default, update.State, P2, new AnswerDealCommand(deal, DealAnswer.Accept)).ShouldFailWith(RuleViolations.DealNotFound);
    }

    [Fact]
    public void Execute_CountersBackAndForth_SwitchWhoAnswers()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        var countered = Execute(p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Counter, Amounts((TestRulesets.Food, 4)), Amounts((TestRulesets.Stone, 1))));
        var p1Turn = Execute(countered.State, P2, new EndTurnCommand()).State;
        var again = Execute(p1Turn, P1, new AnswerDealCommand(deal, DealAnswer.Counter, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1))));
        var p2Again = Execute(again.State, P1, new EndTurnCommand()).State;
        var accepted = Execute(p2Again, P2, new AnswerDealCommand(deal, DealAnswer.Accept));

        countered.State.FindDeal(deal)!.ShouldSatisfyAllConditions(
            static offer => offer.Status.ShouldBe(TradeDealStatus.Countered),
            static offer => offer.Answerer.ShouldBe(P1),
            static offer => offer.Terms.Gives.ShouldBe(Amounts((TestRulesets.Food, 4))));
        countered.Events.ShouldHaveSingleItem().ShouldBeOfType<TradeDealChangedEvent>().Change.ShouldBe(TradeChange.Countered);
        GameEngine.Execute(TestRulesets.Default, countered.State, P2, new AnswerDealCommand(deal, DealAnswer.Accept)).ShouldFailWith(RuleViolations.NoProposal);
        again.State.FindDeal(deal)!.Status.ShouldBe(TradeDealStatus.Proposed);
        accepted.State.FindDeal(deal)!.ShouldSatisfyAllConditions(
            static agreed => agreed.Status.ShouldBe(TradeDealStatus.Travelling),
            static agreed => agreed.Terms.Gives.ShouldBe(Amounts((TestRulesets.Food, 3))));
    }

    [Fact]
    public void Execute_OwnerAcceptsACounterOnTheirTurn_TheTraderSetsOffAtOnce()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;
        var countered = Execute(p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Counter, Amounts((TestRulesets.Food, 4)), Amounts((TestRulesets.Stone, 1)))).State;
        var p1Turn = Execute(countered, P2, new EndTurnCommand()).State;

        var update = Execute(p1Turn, P1, new AnswerDealCommand(deal, DealAnswer.Accept));

        update.State.FindUnit(scenario.Trader)!.Position.ShouldBe(At(5, 2));
    }

    [Fact]
    public void Execute_CounterOutsideTheMarkets_IsRejected()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p2Turn, P2, new AnswerDealCommand(deal, DealAnswer.Counter, Amounts((TestRulesets.Food, 7)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.HomeMarketFull);
    }

    [Fact]
    public void Execute_ProposerAnsweringItsOwnOffer_HasNoOfferToAnswer()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();

        GameEngine.Execute(TestRulesets.Default, state, P1, new AnswerDealCommand(deal, DealAnswer.Accept)).ShouldFailWith(RuleViolations.NoProposal);
    }

    [Fact]
    public void Execute_UnknownAnswer_IsRejected()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p2Turn, P2, new AnswerDealCommand(deal, (DealAnswer)42)).ShouldFailWith(RuleViolations.UnknownAnswer);
    }

    [Fact]
    public void Execute_BetterOfferOverTheCap_IsAcceptedOnlyAfterCancellingAnOlderDeal()
    {
        // The older deal is listed twice: it counts once, as in the client's preview.
        // The host sells 4 Stone: P2's own deal from another city already takes 4, so P1's offer needs that room.
        var scenario = new TradeScenario();
        var p2Home = scenario.Builder.AddCity(P2, 8, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var p2Trader = scenario.Builder.AddUnit(P2, TestRulesets.Trader, 8, 7, homeCity: p2Home);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (8, 6))
            .WithCity(p2Home, static city => city with { Market = [Order(TestRulesets.Gold, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 4)] })
            .WithCity(scenario.Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Gold, MarketSide.Buy, 5), Order(TestRulesets.Stone, MarketSide.Sell, 4)] });
        var (state, deal) = scenario.Proposed();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;
        var ownDeal = Execute(p2Turn, P2, new ProposeDealCommand(p2Trader, scenario.Host, Amounts((TestRulesets.Gold, 2)), Amounts((TestRulesets.Stone, 4)))).State;
        var own = ownDeal.TradeDealList.Single(other => other.Owner == P2).Id;

        var full = GameEngine.Execute(TestRulesets.Default, ownDeal, P2, new AnswerDealCommand(deal, DealAnswer.Accept));
        var notThatOne = GameEngine.Execute(TestRulesets.Default, ownDeal, P2, new AnswerDealCommand(deal, DealAnswer.Accept, CancelDeals: [deal]));
        var update = Execute(ownDeal, P2, new AnswerDealCommand(deal, DealAnswer.Accept, CancelDeals: [own, own]));

        ownDeal.FindDeal(own)!.IsAgreed.ShouldBeTrue();
        full.ShouldFailWith(RuleViolations.HostMarketFull);
        notThatOne.ShouldFailWith(RuleViolations.CannotCancelDeal);
        update.State.FindDeal(deal)!.IsAgreed.ShouldBeTrue();
        update.State.FindDeal(own).ShouldBeNull();
        update.Events.OfType<TradeDealChangedEvent>().Select(static changed => (changed.DealId, changed.Change))
            .ShouldBe([(own, TradeChange.Cancelled), (deal, TradeChange.Accepted)]);
    }
}
