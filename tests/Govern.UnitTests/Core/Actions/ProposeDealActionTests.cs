using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Actions;

public class ProposeDealActionTests
{
    [Fact]
    public void Execute_TermsWithinBothMarkets_WaitForTheHost()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Opened();

        var update = Execute(state, P1, new ProposeDealCommand(route, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1))));

        var deal = update.State.FindRoute(route)!.Deal.ShouldNotBeNull();
        deal.Accepted.ShouldBeFalse();
        deal.Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
        deal.Receives.ShouldBe(Amounts((TestRulesets.Stone, 1)));
        update.Events.OfType<TradeRouteChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Proposed);
    }

    [Fact]
    public void Execute_DealWithOwnCity_IsAcceptedAtOnceAndTheTraderTurnsHome()
    {
        var scenario = new TradeScenario(hostOwner: P1);
        var (state, route) = scenario.Opened();

        var update = Execute(state, P1, new ProposeDealCommand(route, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1))));

        update.State.FindRoute(route)!.IsActive.ShouldBeTrue();
        update.State.FindRoute(route)!.Deal!.AcceptedTurn.ShouldBe(state.Turn);
        update.State.FindUnit(scenario.Trader)!.Destination.ShouldBe(At(3, 2));
        update.Events.OfType<TradeRouteChangedEvent>().Select(static changed => changed.Change).ShouldBe([TradeChange.Proposed, TradeChange.Accepted]);
    }

    [Fact]
    public void Execute_TraderStillOnItsWay_IsRejected()
    {
        var scenario = new TradeScenario();
        var state = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;
        var route = state.TradeRouteList.Single().Id;

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProposeDealCommand(route, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.TraderNotAtPost);
    }

    public static TheoryData<string, string, int, string, int> BadTerms => new()
    {
        { "trade.empty_deal", "food", 3, "stone", 0 },
        { "trade.not_tradable", "water", 3, "stone", 1 },
        { "trade.bad_amount", "food", 21, "stone", 1 },
        { "trade.bad_amount", "food", 0, "stone", 1 },
        { "trade.home_market", "gold", 1, "stone", 1 },
        { "trade.home_market", "food", 3, "gold", 1 },
        { "trade.host_market", "food", 7, "stone", 1 },
        { "trade.host_market", "food", 3, "stone", 5 },
        { "trade.host_market", "wood", 1, "stone", 1 },
    };

    [Theory]
    [MemberData(nameof(BadTerms))]
    public void Execute_TermsOutsideTheMarkets_AreRejected(string code, string gives, int givesAmount, string receives, int receivesAmount)
    {
        // The home city sells and buys more than the host, so the host's market is what stops the bigger offers.
        var scenario = new TradeScenario();
        scenario.Builder.WithCity(scenario.Home, static city => city with
        {
            Market = [Order(TestRulesets.Food, MarketSide.Sell, 20), Order(TestRulesets.Wood, MarketSide.Sell, 5), Order(TestRulesets.Stone, MarketSide.Buy, 20)],
        });
        var (state, route) = scenario.Opened();
        var asked = receivesAmount == 0 ? Amounts() : Amounts((new ResourceType(receives), receivesAmount));

        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new ProposeDealCommand(route, Amounts((new ResourceType(gives), givesAmount)), asked));

        result.Violation.ShouldNotBeNull().Code.ShouldBe(code);
    }

    [Fact]
    public void Execute_RouteOfAnotherPlayer_IsNotFound()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Opened();
        var p2Turn = Execute(state, P1, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p2Turn, P2, new ProposeDealCommand(route, Amounts((TestRulesets.Stone, 1)), Amounts((TestRulesets.Food, 3))))
            .ShouldFailWith(RuleViolations.RouteNotFound);
    }

    [Fact]
    public void Execute_ActiveDeal_IsRejected()
    {
        var scenario = new TradeScenario(hostOwner: P1);
        var (state, route) = scenario.Proposed();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProposeDealCommand(route, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.DealActive);
    }
}
