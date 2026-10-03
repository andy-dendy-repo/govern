using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Serialization;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Visibility;

public class TradeRouteViewTests
{
    [Fact]
    public void ViewFor_RouteOnItsWay_IsSeenByItsOwnerOnly()
    {
        var scenario = new TradeScenario();
        var state = Execute(scenario.Builder.Build(), P1, new OpenTradeRouteCommand(scenario.Trader, scenario.Home, scenario.Host)).State;

        var own = GameEngine.ViewFor(TestRulesets.Default, state, P1).TradeRouteList.ShouldHaveSingleItem();

        (own.Trader, own.HostPost, own.Opened, own.HostMarket).ShouldBe((scenario.Trader, At(7, 2), false, null));
        GameEngine.ViewFor(TestRulesets.Default, state, P2).TradeRoutes.ShouldBeNull();
        GameEngine.ViewFor(TestRulesets.Default, state, P2).Units.ShouldAllBe(static unit => unit.RouteId == null);
    }

    [Fact]
    public void ViewFor_OpenRoute_ShowsTheOwnerTheHostsFreeMarketAndTheHostTheTermsOnly()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Proposed();

        var owner = GameEngine.ViewFor(TestRulesets.Default, state, P1).TradeRouteList.ShouldHaveSingleItem();
        var host = GameEngine.ViewFor(TestRulesets.Default, state, P2).TradeRouteList.ShouldHaveSingleItem();

        owner.HostMarket.ShouldBe([Order(TestRulesets.Food, MarketSide.Buy, 6), Order(TestRulesets.Stone, MarketSide.Sell, 4)]);
        owner.HostDemand.ShouldNotBeNull().ShouldNotBeEmpty();
        (host.Id, host.Owner, host.HomeName, host.Opened).ShouldBe((route, P1, state.FindCity(scenario.Home)!.Name, true));
        host.Deal.ShouldNotBeNull().Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
        (host.Trader, host.HostPost, host.HostMarket, host.HostDemand).ShouldBe((null, null, null, null));
    }

    [Fact]
    public void ViewFor_TraderLeftTheHostsPost_HidesTheHostsMarketAgain()
    {
        var scenario = new TradeScenario();
        var (accepted, _) = scenario.Accepted();

        var walkedHome = Execute(accepted, P2, new EndTurnCommand()).State;

        var owner = GameEngine.ViewFor(TestRulesets.Default, walkedHome, P1).TradeRouteList.ShouldHaveSingleItem();
        walkedHome.FindUnit(scenario.Trader)!.Position.DistanceTo(At(7, 2)).ShouldBeGreaterThan(1);
        (owner.IsActive, owner.HostMarket, owner.HostDemand).ShouldBe((true, null, null));
    }

    [Fact]
    public void ViewFor_PausedRoute_ShowsThePauseToItsOwnerOnly()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Accepted();
        var paused = state with { TradeRoutes = [state.FindRoute(route)! with { Paused = true }] };

        GameEngine.ViewFor(TestRulesets.Default, paused, P1).TradeRouteList.ShouldHaveSingleItem().Paused.ShouldBeTrue();
        GameEngine.ViewFor(TestRulesets.Default, paused, P2).TradeRouteList.ShouldHaveSingleItem().Paused.ShouldBeFalse();
    }

    [Fact]
    public void ViewFor_Cities_ShowTheMarketToTheOwnerAndAVisibleTradePostToEveryone()
    {
        var scenario = new TradeScenario();
        var state = scenario.Builder.Build();

        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);
        var blind = GameEngine.ViewFor(TestRulesets.Default, state with { Units = [.. state.Units.Where(static unit => unit.Type != TestRulesets.Scout)] }, P1);

        var home = view.Cities.Single(city => city.Id == scenario.Home);
        (home.TradePost, home.MarketList.Length).ShouldBe((At(3, 2), 2));
        var host = view.Cities.Single(city => city.Id == scenario.Host);
        (host.TradePost, host.Market, host.Demand).ShouldBe((At(7, 2), null, null));
        blind.Cities.ShouldNotContain(city => city.Id == scenario.Host);
    }

    [Fact]
    public void GameSerializer_StateWithRoutesAndMarkets_RoundTrips()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Accepted();

        var json = GameSerializer.Serialize(state);

        GameSerializer.Serialize(GameSerializer.DeserializeState(json)).ShouldBe(json);
        json.ShouldContain("\"tradeRoutes\"");
        GameSerializer.Serialize(scenario.Builder.Build() with { Cities = [] }).ShouldNotContain("tradeRoutes");
    }
}
