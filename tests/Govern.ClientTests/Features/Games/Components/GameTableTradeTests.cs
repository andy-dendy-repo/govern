using System.Text.Json.Nodes;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>Trading on the game screen (G17, G17b): the Market tab, the city's traders and their deals, answering offers, and the trader's orders.</summary>
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
    public void HostedOffer_OverTheCap_IsAcceptedAfterTickingTheOlderDealToCancel()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var host = Home with { Market = [new MarketOrder(Food, MarketSide.Buy, 6), new MarketOrder(Stone, MarketSide.Sell, 4)] };
        var older = new TradeDealView(new TradeDealId(20), You, new CityId(8), "Pinecrest", host.Id, host.Name, You,
            Terms([(Food, 3)], [(Stone, 4)]), TradeDealStatus.Active);
        var offer = new TradeDealView(new TradeDealId(21), Opponent, Enemy.Id, Enemy.Name, host.Id, host.Name, You,
            Terms([(Food, 2)], [(Stone, 1)]), TradeDealStatus.Proposed);
        var cut = RenderTable(Create(cities: [host, City(8, You, At(6, 6), "Pinecrest")]) with { TradeDeals = [older, offer], TradePartners = [Partner()] });
        _board.Click(At(2, 2));

        var hosted = cut.Find(".hosted-deal-21");
        hosted.QuerySelector(".hosted-status")!.TextContent.ShouldBe("Rivertown (Seat 2) offers: you give 1 stone, get 2 food a turn.");
        hosted.QuerySelector("button.accept-deal")!.HasAttribute("disabled").ShouldBeTrue();
        hosted.QuerySelector(".accept-reason")!.TextContent.ShouldBe("The host city's market doesn't sell or buy that much.");
        cut.Find(".hosted-deal-21 .cancel-to-make-room input").Change(true);
        cut.Find(".hosted-deal-21 button.accept-deal").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"answer_deal","dealId":{"value":21},"answer":"accept","cancelDeals":[{"value":20}]}"""));
        cut.Find(".city-tabs [role=tab] .rz-tabview-icon").TextContent.ShouldBe("notifications");
    }

    [Fact]
    public void FreeTrader_SuggestsADealToAKnownPartnerAndSendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var trader = Unit(5, You, TraderType, At(2, 3)) with { HomeCity = Home.Id };
        var partner = Partner() with { Market = [new MarketOrder(Food, MarketSide.Buy, 6), new MarketOrder(Stone, MarketSide.Sell, 4)], Demand = [new ResourceDemand(Stone, 150)] };
        var cut = RenderTable(Create([trader], [Home]) with { TradePartners = [partner] });
        _board.Click(At(2, 2));

        cut.Find(".city-trader-5 .trader-status").TextContent.ShouldBe("Free: no deal.");
        cut.Find(".city-trader-5 button.suggest-deal").Click();
        cut.Find(".city-trader-5 .proposal-city-9").Click();
        cut.Find(".proposal-value").TextContent.ShouldBe("Rivertown values what it gets at 1 against 3 it gives.");
        cut.Find("button.proposal-fair").Click();
        cut.Find(".proposal-value").TextContent.ShouldBe("Rivertown values what it gets at 3 against 3 it gives.");
        cut.Find("button.propose-deal").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe(
            """{"type":"propose_deal","traderId":{"value":5},"hostId":{"value":9},"gives":[{"resource":"food","amount":3}],"receives":[{"resource":"stone","amount":1}]}"""));
    }

    [Fact]
    public void CounterOffer_ToTheOwnersTrader_CanBeAcceptedOrDeclined()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var deal = new TradeDealView(new TradeDealId(22), You, Home.Id, Home.Name, Enemy.Id, Enemy.Name, Opponent,
            Terms([(Food, 4)], [(Stone, 1)]), TradeDealStatus.Countered, Trader: new UnitId(5));
        var trader = Unit(5, You, TraderType, At(2, 3)) with { HomeCity = Home.Id };
        var partner = Partner() with { Market = [new MarketOrder(Food, MarketSide.Buy, 6), new MarketOrder(Stone, MarketSide.Sell, 4)] };
        var cut = RenderTable(Create([trader], [Home]) with { TradeDeals = [deal], TradePartners = [partner] });
        _board.Click(At(2, 2));

        cut.Find(".city-trader-5 .trader-status").TextContent.ShouldBe("Rivertown (Seat 2) offers instead: you give 4 food, get 1 stone a turn.");
        cut.Find(".city-trader-5 button.accept-deal").HasAttribute("disabled").ShouldBeFalse();
        cut.Find(".city-trader-5 button.decline-deal").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe("""{"type":"answer_deal","dealId":{"value":22},"answer":"decline"}"""));
    }

    [Fact]
    public void Trader_Selected_SaysItsHomeAndOpensItsMarket()
    {
        var trader = Unit(5, You, TraderType, At(2, 3)) with { HomeCity = Home.Id };
        var cut = RenderTable(Create([trader], [Home]));
        _board.Click(At(2, 3));

        cut.Find(".trader-deal").TextContent.ShouldBe("From Oakhaven, free for a deal.");
        cut.Find("button.trader-open-market").TextContent.ShouldContain("Make a deal");
        cut.Find("button.trader-open-market").Click();

        cut.Find(".city-traders .city-trader-5").ShouldNotBeNull();
        cut.Find(".hud-selection").ClassList.ShouldContain("hud-selection-city");
    }

    [Fact]
    public void Trader_OnADeal_TakesNoOtherOrder()
    {
        var deal = new TradeDealView(new TradeDealId(22), You, Home.Id, Home.Name, Enemy.Id, Enemy.Name, Opponent,
            Terms([(Food, 3)], [(Stone, 1)]), TradeDealStatus.Active, Trader: new UnitId(5));
        var trader = Unit(5, You, TraderType, At(4, 2)) with { HomeCity = Home.Id, DealId = deal.Id, Destination = At(7, 2) };
        var cut = RenderTable(Create([trader], [Home, Enemy]) with { TradeDeals = [deal] });

        _board.Click(At(4, 2));

        cut.Find(".trader-deal").TextContent.ShouldBe("From Oakhaven, trading with Rivertown.");
        cut.FindAll("button.go-to").ShouldBeEmpty();
        cut.FindAll("button.stop-order").ShouldBeEmpty();
        cut.Find("button.trader-open-market").TextContent.ShouldContain("Open market");
        cut.Find(".hud-selection").ClassList.ShouldNotContain("hud-selection-city");
    }

    private static TradePartnerView Partner() =>
        new(Enemy.Id, Opponent, Enemy.Name, At(7, 2), [new MarketOrder(Food, MarketSide.Sell, 6), new MarketOrder(Stone, MarketSide.Buy, 4)], null);

    private static TradeTerms Terms((ResourceType Resource, int Amount)[] gives, (ResourceType Resource, int Amount)[] receives) =>
        new([.. gives.Select(static part => new ResourceAmount(part.Resource, part.Amount))], [.. receives.Select(static part => new ResourceAmount(part.Resource, part.Amount))]);

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
