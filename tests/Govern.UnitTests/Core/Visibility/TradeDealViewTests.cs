using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Serialization;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Visibility;

public class TradeDealViewTests
{
    [Fact]
    public void ViewFor_Offer_IsSeenByBothSidesWithTheTraderForItsOwnerOnly()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Proposed();

        var owner = GameEngine.ViewFor(TestRulesets.Default, state, P1).TradeDealList.ShouldHaveSingleItem();
        var host = GameEngine.ViewFor(TestRulesets.Default, state, P2).TradeDealList.ShouldHaveSingleItem();

        (owner.Id, owner.Trader, owner.Status, owner.HostName).ShouldBe((deal, scenario.Trader, TradeDealStatus.Proposed, state.FindCity(scenario.Host)!.Name));
        (host.Id, host.Owner, host.HomeName, host.Trader, host.Answerer).ShouldBe((deal, P1, state.FindCity(scenario.Home)!.Name, null, P2));
        host.Terms.Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
    }

    [Fact]
    public void ViewFor_PausedDeal_ShowsThePauseToItsOwnerOnly()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Active();
        var paused = state with { TradeDeals = [state.FindDeal(deal)! with { Paused = true }] };

        GameEngine.ViewFor(TestRulesets.Default, paused, P1).TradeDealList.ShouldHaveSingleItem().Paused.ShouldBeTrue();
        GameEngine.ViewFor(TestRulesets.Default, paused, P2).TradeDealList.ShouldHaveSingleItem().Paused.ShouldBeFalse();
    }

    [Fact]
    public void ViewFor_HomeCityOfATrader_IsShownToItsOwnerOnly()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Active();

        GameEngine.ViewFor(TestRulesets.Default, state, P1).FindOwnUnit(scenario.Trader)!.HomeCity.ShouldBe(scenario.Home);
        GameEngine.ViewFor(TestRulesets.Default, state, P2).Units.Single(unit => unit.Id == scenario.Trader).HomeCity.ShouldBeNull();
    }

    [Fact]
    public void ViewFor_SeenTradePost_MakesItsCityAPartnerWithItsMarket()
    {
        var scenario = new TradeScenario();
        var state = scenario.Builder.Build();

        var partner = GameEngine.ViewFor(TestRulesets.Default, state, P1).TradePartnerList.ShouldHaveSingleItem();

        (partner.Id, partner.Owner, partner.TradePost).ShouldBe((scenario.Host, P2, At(7, 2)));
        partner.MarketList.ShouldBe([Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Stone, MarketSide.Sell, 4)]);
        state.FindPlayer(P1)!.KnownPostList.ShouldBe([scenario.Host]);
        GameEngine.ViewFor(TestRulesets.Default, state, P2).TradePartners.ShouldBeNull();
    }

    [Fact]
    public void ViewFor_PartnerOutOfSight_StaysAPartner()
    {
        var scenario = new TradeScenario();
        var seen = scenario.Builder.Build();
        var blind = seen with { Units = [.. seen.Units.Where(static unit => unit.Type != TestRulesets.Scout)] };

        var view = GameEngine.ViewFor(TestRulesets.Default, blind, P1);

        view.Cities.ShouldNotContain(city => city.Id == scenario.Host);
        view.TradePartnerList.ShouldHaveSingleItem().Id.ShouldBe(scenario.Host);
    }

    [Fact]
    public void ViewFor_PartnersOtherDeals_ComeOffItsMarketButTheViewersOwnDoNot()
    {
        // P2's own deal from a second city takes 2 of the host's 4 Stone (and 1 of its Gold); P1's agreed deal takes 1 more, which P1 subtracts itself.
        var scenario = new TradeScenario();
        var p2Home = scenario.Builder.AddCity(P2, 8, 7, stock: Plenty, paid: true, buildings: TestRulesets.TradePost);
        var p2Trader = scenario.Builder.AddUnit(P2, TestRulesets.Trader, 8, 7, homeCity: p2Home);
        scenario.Builder.WithBuilding(TestRulesets.TradePost, (8, 6))
            .WithCity(p2Home, static city => city with { Market = [Order(TestRulesets.Stone, MarketSide.Buy, 4), Order(TestRulesets.Gold, MarketSide.Sell, 5)] })
            .WithCity(scenario.Host, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Stone, MarketSide.Sell, 4), Order(TestRulesets.Gold, MarketSide.Buy, 5)] });
        var (accepted, _) = scenario.Accepted();
        var both = Execute(accepted, P2, new ProposeDealCommand(p2Trader, scenario.Host, Amounts((TestRulesets.Gold, 1)), Amounts((TestRulesets.Stone, 2)))).State;

        var view = GameEngine.ViewFor(TestRulesets.Default, both, P1);

        view.TradePartnerList.Single(partner => partner.Id == scenario.Host).MarketList
            .ShouldBe([Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Stone, MarketSide.Sell, 2), Order(TestRulesets.Gold, MarketSide.Buy, 4)]);
        view.TradeDealList.ShouldHaveSingleItem();
    }

    [Fact]
    public void ViewFor_Cities_ShowTheMarketToTheOwnerAndAVisibleTradePostToEveryone()
    {
        var scenario = new TradeScenario();
        var state = scenario.Builder.Build();

        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        var home = view.Cities.Single(city => city.Id == scenario.Home);
        (home.TradePost, home.MarketList.Length).ShouldBe((At(3, 2), 2));
        var host = view.Cities.Single(city => city.Id == scenario.Host);
        (host.TradePost, host.Market, host.Demand).ShouldBe((At(7, 2), null, null));
    }

    [Fact]
    public void GameSerializer_StateWithDealsAndMarkets_RoundTrips()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Active();

        var json = GameSerializer.Serialize(state);

        GameSerializer.Serialize(GameSerializer.DeserializeState(json)).ShouldBe(json);
        json.ShouldContain("\"tradeDeals\"");
        json.ShouldContain("\"homeCity\"");
        GameSerializer.Serialize(scenario.Builder.Build() with { Cities = [] }).ShouldNotContain("tradeDeals");
    }

    [Fact]
    public void GameSerializer_GameSavedWithRoutes_LoadsWithoutThem()
    {
        var scenario = new TradeScenario();
        var json = GameSerializer.Serialize(scenario.Builder.Build());
        var legacy = json.Replace("\"units\":[", "\"tradeRoutes\":[{\"id\":5,\"owner\":\"x\"}],\"units\":[", StringComparison.Ordinal);

        var loaded = GameSerializer.DeserializeState(legacy);

        loaded.TradeDealList.ShouldBeEmpty();
    }
}
