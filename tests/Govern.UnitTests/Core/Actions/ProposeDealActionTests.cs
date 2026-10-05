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

        var update = Execute(scenario.Builder.Build(), P1, new ProposeDealCommand(scenario.Trader, scenario.Host, Amounts((TestRulesets.Food, 3)), Amounts((TestRulesets.Stone, 1))));

        var deal = update.State.TradeDealList.ShouldHaveSingleItem();
        (deal.Owner, deal.Home, deal.Host, deal.HostOwner, deal.Trader, deal.Status).ShouldBe((P1, scenario.Home, scenario.Host, P2, scenario.Trader, TradeDealStatus.Proposed));
        deal.Terms.Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
        deal.Terms.Receives.ShouldBe(Amounts((TestRulesets.Stone, 1)));
        update.State.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.DealId.ShouldBeNull(),
            static trader => trader.Position.ShouldBe(At(2, 2)));
        update.Events.OfType<TradeDealChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Proposed);
    }

    [Fact]
    public void Execute_OffersUseNoMarket_SoASecondTraderCanOfferTheSame()
    {
        var scenario = new TradeScenario();
        var second = scenario.Builder.AddUnit(P1, TestRulesets.Trader, 2, 2, homeCity: scenario.Home);
        var (state, _) = scenario.Proposed();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new ProposeDealCommand(second, scenario.Host, Amounts((TestRulesets.Food, 6)), Amounts((TestRulesets.Stone, 4))));

        update.ShouldSucceed().State.TradeDealList.Length.ShouldBe(2);
    }

    [Fact]
    public void Execute_DealWithOwnCity_IsAcceptedAtOnceAndTheTraderSetsOff()
    {
        var scenario = new TradeScenario(hostOwner: P1);

        var (state, deal) = scenario.Proposed();

        state.FindDeal(deal)!.Status.ShouldBe(TradeDealStatus.Travelling);
        state.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            trader => trader.DealId.ShouldBe(deal),
            static trader => trader.Destination.ShouldBe(At(7, 2)),
            static trader => trader.Position.ShouldBe(At(5, 2)));
    }

    [Fact]
    public void Execute_TraderWithADeal_IsRejected()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Proposed();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProposeDealCommand(scenario.Trader, scenario.Host, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.TraderHasDeal);
    }

    [Fact]
    public void Execute_NotATrader_IsRejected()
    {
        var scenario = new TradeScenario();
        var soldier = scenario.Builder.AddUnit(P1, TestRulesets.Soldier, 2, 3);

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new ProposeDealCommand(soldier, scenario.Host, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.CannotTrade);
    }

    [Fact]
    public void Execute_HostWhosePostWasNeverSeen_IsNoPartner()
    {
        // Without the scout P1 never saw the host's post, so the host reads like no partner at all.
        var builder = new GameStateBuilder();
        var home = builder.AddCity(P1, 2, 2, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var host = builder.AddCity(P2, 9, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        builder.WithBuilding(TestRulesets.TradePost, (3, 2)).WithBuilding(TestRulesets.TradePost, (9, 6));
        var trader = builder.AddUnit(P1, TestRulesets.Trader, 2, 2, homeCity: home);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProposeDealCommand(trader, host, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.NoTradePartner);
    }

    [Fact]
    public void Execute_ToItsOwnHome_IsNoPartner()
    {
        var scenario = new TradeScenario();

        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new ProposeDealCommand(scenario.Trader, scenario.Home, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.NoTradePartner);
    }

    [Fact]
    public void Execute_TraderWithoutAHome_IsRejected()
    {
        var scenario = new TradeScenario();
        var state = scenario.Builder.Build();
        var homeless = ChangeUnit(state, scenario.Trader, static trader => trader with { HomeCity = null });

        GameEngine.Execute(TestRulesets.Default, homeless, P1, new ProposeDealCommand(scenario.Trader, scenario.Host, Amounts((TestRulesets.Food, 1)), Amounts((TestRulesets.Stone, 1))))
            .ShouldFailWith(RuleViolations.NoHomeCity);
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
        var asked = receivesAmount == 0 ? Amounts() : Amounts((new ResourceType(receives), receivesAmount));

        var result = GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new ProposeDealCommand(scenario.Trader, scenario.Host, Amounts((new ResourceType(gives), givesAmount)), asked));

        result.Violation.ShouldNotBeNull().Code.ShouldBe(code);
    }

    [Fact]
    public void Execute_TraderOfAnotherPlayer_IsNotFound()
    {
        var scenario = new TradeScenario();
        var p2Turn = Execute(scenario.Builder.Build(), P1, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p2Turn, P2, new ProposeDealCommand(scenario.Trader, scenario.Home, Amounts((TestRulesets.Stone, 1)), Amounts((TestRulesets.Food, 3))))
            .ShouldFailWith(RuleViolations.UnitNotFound);
    }

    [Fact]
    public void Execute_TraderPassingUnitsAndCities_ReachesTheHostPost()
    {
        // An enemy soldier and the host city itself stand on the way; the trader walks through both.
        var scenario = new TradeScenario(hostOwner: P1);
        scenario.Builder.AddUnit(P2, TestRulesets.Soldier, 4, 2);
        scenario.Builder.AddUnit(P1, TestRulesets.Soldier, 5, 2);

        var (state, _) = scenario.Proposed();

        state.FindUnit(scenario.Trader)!.Position.ShouldBe(At(5, 2));
        state.UnitsAt(At(5, 2)).Select(static unit => unit.Type).ShouldBe([TestRulesets.Soldier, TestRulesets.Trader], ignoreOrder: true);
    }
}
