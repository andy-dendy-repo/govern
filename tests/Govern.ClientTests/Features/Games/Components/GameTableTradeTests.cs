using System.Text.Json.Nodes;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>Trading on the game screen (G17): the Market tab, answering and making proposals, and the trader's orders.</summary>
public class GameTableTradeTests : ClientTestContext
{
    private static readonly CityView Home = City(7, You, At(2, 2), "Oakhaven") with
    {
        Buildings = [TradePost],
        TradePost = At(3, 2),
        Market = [new MarketOrder(Food, MarketSide.Sell, 6), new MarketOrder(Stone, MarketSide.Buy, 4)],
    };

    private static readonly CityView Enemy = City(9, Opponent, At(8, 2), "Rivertown") with { TradePost = At(7, 2) };

    private readonly FakeBoardRenderer _board;

    public GameTableTradeTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void Market_CityWithoutATradePost_SaysItNeedsOneAndCantTrainTraders()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        cut.Find(".market-needs-post").TextContent.ShouldContain("Build a Trade post to trade");
        cut.Find("button.produce-trader").HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll(".market-order").ShouldBeEmpty();
    }

    [Fact]
    public void Market_Orders_ShowTheSideTheCapUsedAndTheRate()
    {
        var city = Home with { Demand = [new ResourceDemand(Stone, 150)] };
        var cut = RenderTable(WithNodes(Create(cities: [city]), 1, "trade"));

        _board.Click(At(2, 2));

        cut.FindAll(".market-order").Select(static row => row.ClassList[^1]).ShouldBe(["market-order-food", "market-order-wood", "market-order-stone", "market-order-gold"]);
        cut.Find(".market-order-food .market-side-sell").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find(".market-order-food .market-used").TextContent.ShouldBe("0 of 6 used");
        cut.Find(".market-order-stone .market-rate").TextContent.ShouldBe("Demand 150%, 1 = 3 Food");
        cut.FindAll(".market-order-water, .market-order-culture").ShouldBeEmpty();
        cut.Find("button.produce-trader").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Market_NewOrderSet_SendsTheCommand()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create(cities: [Home]));
        _board.Click(At(2, 2));

        cut.Find(".market-order-gold button.market-set").HasAttribute("disabled").ShouldBeTrue("nothing changed yet");
        cut.Find(".market-order-gold button.market-side-buy").Click();
        cut.Find(".market-order-gold button.market-set").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"set_market_order","cityId":{"value":7},"resource":"gold","side":"buy","cap":5}"""));
    }

    [Fact]
    public void Proposal_OverTheCap_IsAcceptedAfterTickingTheOlderDealToCancel()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var host = Home with { Market = [new MarketOrder(Food, MarketSide.Buy, 6), new MarketOrder(Stone, MarketSide.Sell, 4)] };
        var older = new TradeRouteView(new TradeRouteId(20), You, new CityId(8), "Pinecrest", host.Id, host.Name, You, Opened: true,
            Deal: new TradeDeal([new ResourceAmount(Food, 3)], [new ResourceAmount(Stone, 4)], Accepted: true, AcceptedTurn: 2));
        var offer = new TradeRouteView(new TradeRouteId(21), Opponent, Enemy.Id, Enemy.Name, host.Id, host.Name, You, Opened: true,
            Deal: new TradeDeal([new ResourceAmount(Food, 2)], [new ResourceAmount(Stone, 1)]));
        var cut = RenderTable(Create(cities: [host, City(8, You, At(6, 6), "Pinecrest")]) with { TradeRoutes = [older, offer] });
        _board.Click(At(2, 2));

        var route = cut.Find(".trade-route-21");
        route.QuerySelector(".trade-route-title")!.TextContent.ShouldBe("From Rivertown (Seat 2)");
        route.QuerySelector(".trade-route-status")!.TextContent.ShouldBe("Proposed: gives 1 stone, gets 2 food a turn.");
        route.QuerySelector("button.accept-deal")!.HasAttribute("disabled").ShouldBeTrue();
        route.QuerySelector(".accept-reason")!.TextContent.ShouldBe("The host city's market doesn't sell or buy that much.");
        cut.Find(".trade-route-21 .cancel-to-make-room input").Change(true);
        cut.Find(".trade-route-21 button.accept-deal").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"answer_deal","routeId":{"value":21},"accept":true,"cancelRoutes":[{"value":20}]}"""));
        cut.Find(".city-tabs [role=tab] .rz-tabview-icon").TextContent.ShouldBe("notifications");
    }

    [Fact]
    public void Proposal_FromTheHomeCity_ShowsTheHostsValuesAndSendsTheDeal()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var route = new TradeRouteView(new TradeRouteId(22), You, Home.Id, Home.Name, Enemy.Id, Enemy.Name, Opponent, Opened: true,
            Trader: new UnitId(5), HostPost: At(7, 2), HostMarket: [new MarketOrder(Food, MarketSide.Buy, 6), new MarketOrder(Stone, MarketSide.Sell, 4)],
            HostDemand: [new ResourceDemand(Stone, 150)]);
        var trader = Unit(5, You, TraderType, At(6, 2)) with { RouteId = route.Id };
        var cut = RenderTable(Create([trader], [Home, Enemy]) with { TradeRoutes = [route] });
        _board.Click(At(2, 2));

        cut.Find(".trade-route-22 .trade-route-status").TextContent.ShouldBe("The trader waits at the trade post for your proposal.");
        cut.Find(".proposal-value").TextContent.ShouldBe("Rivertown values your offer at 1 against 3 asked.");
        cut.Find("button.proposal-fair").Click();
        cut.Find(".proposal-value").TextContent.ShouldBe("Rivertown values your offer at 3 against 3 asked.");
        cut.Find("button.propose-deal").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe(
            """{"type":"propose_deal","routeId":{"value":22},"gives":[{"resource":"food","amount":3}],"receives":[{"resource":"stone","amount":1}]}"""));
    }

    [Fact]
    public void Trader_Selected_OpensARouteToThePickedCity()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create([Unit(5, You, TraderType, At(2, 3))], [Home, Enemy]));
        _board.Click(At(2, 3));

        cut.Find(".trader-pick-from .trader-city-7").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find(".trader-open-reason").TextContent.ShouldBe("Pick the city to trade with: one with a trade post you can see.");
        cut.Find(".trader-pick-to .trader-city-9").Click();
        cut.FindAll(".trader-open-reason").ShouldBeEmpty();
        cut.Find("button.trader-open-route").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"open_trade_route","traderId":{"value":5},"homeId":{"value":7},"hostId":{"value":9}}"""));
    }

    [Fact]
    public void Trader_OnARoute_CanOnlyCloseIt()
    {
        var route = new TradeRouteView(new TradeRouteId(22), You, Home.Id, Home.Name, Enemy.Id, Enemy.Name, Opponent, Trader: new UnitId(5), HostPost: At(7, 2));
        var trader = Unit(5, You, TraderType, At(4, 2)) with { RouteId = route.Id, Destination = At(7, 2) };
        var cut = RenderTable(Create([trader], [Home, Enemy]) with { TradeRoutes = [route] });

        _board.Click(At(4, 2));

        cut.Find(".trader-route").TextContent.Trim().ShouldBe("On the route from Oakhaven to Rivertown; make deals in Oakhaven's Market tab.");
        cut.FindAll("button.go-to").ShouldBeEmpty();
        cut.FindAll("button.stop-order").ShouldBeEmpty();
        cut.Find("button.trader-close-route").ShouldNotBeNull();
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
