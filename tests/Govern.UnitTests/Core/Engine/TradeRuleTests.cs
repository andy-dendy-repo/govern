using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Engine;

public class TradeRuleTests
{
    [Fact]
    public void EndTurn_HostOwnersTurnWithAnActiveDeal_SwapsBothParts()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Active();
        var before = StockOf(state, scenario.Home, TestRulesets.Stone);

        var update = Execute(state, P2, new EndTurnCommand());

        StockOf(update.State, scenario.Home, TestRulesets.Stone).ShouldBe(before + 1);
        var exchanged = update.Events.OfType<TradeExchangedEvent>().ShouldHaveSingleItem();
        (exchanged.DealId, exchanged.HomeShort, exchanged.HostShort).ShouldBe((deal, false, false));
        exchanged.Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
    }

    [Fact]
    public void EndTurn_TraderStillTravelling_SwapsNothing()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Accepted();

        var update = Execute(state, P2, new EndTurnCommand());

        update.Events.OfType<TradeExchangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void EndTurn_OwnersTurn_SwapsNothing()
    {
        var scenario = new TradeScenario();
        var (active, _) = scenario.Active();
        var p1Turn = Execute(active, P2, new EndTurnCommand()).State;

        var update = Execute(p1Turn, P1, new EndTurnCommand());

        update.Events.OfType<TradeExchangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void EndTurn_SideWithoutItsPart_SwapsNothingThatTurn()
    {
        var scenario = new TradeScenario();
        scenario.Builder.WithCity(scenario.Host, static city => city with { Stock = Amounts((TestRulesets.Food, 30), (TestRulesets.Water, 30)) });
        var (state, _) = scenario.Active();
        var before = StockOf(state, scenario.Home, TestRulesets.Stone);

        var update = Execute(state, P2, new EndTurnCommand());

        var exchanged = update.Events.OfType<TradeExchangedEvent>().ShouldHaveSingleItem();
        (exchanged.HomeShort, exchanged.HostShort).ShouldBe((false, true));
        StockOf(update.State, scenario.Home, TestRulesets.Stone).ShouldBe(before);
        StockOf(update.State, scenario.Host, TestRulesets.Food).ShouldBe(StockOf(Execute(state with { TradeDeals = null }, P2, new EndTurnCommand()).State, scenario.Host, TestRulesets.Food));
    }

    [Fact]
    public void Walk_ActiveDeal_ShuttlesTheTraderBetweenThePosts()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Active();

        var round4 = Execute(state, P2, new EndTurnCommand()).State;
        var round5 = EndRound(round4);
        var round6 = EndRound(round5);

        round4.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(4, 2)),
            static trader => trader.Destination.ShouldBe(At(3, 2)));
        round5.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(3, 2)),
            static trader => trader.Destination.ShouldBe(At(7, 2)));
        round6.FindUnit(scenario.Trader)!.Position.ShouldBe(At(6, 2));
    }

    [Fact]
    public void Walk_WayBlockedByWater_PausesTheDealUntilItIsFree()
    {
        // A wall of lakes down column 5 with one gap at (5, 2), which turns to lake once the deal is active and back later.
        var scenario = new TradeScenario();
        scenario.Builder.WithTerrain(TestRulesets.Lake, [.. Enumerable.Range(0, Height).Where(static row => row != 2).Select(static row => (5, row))]);
        var (active, deal) = scenario.Active();
        var state = WithTile(active, At(5, 2), TestRulesets.Lake);

        var blocked = Execute(state, P2, new EndTurnCommand()).State;
        var notSwapped = Execute(Execute(blocked, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());
        var cleared = WithTile(notSwapped.State, At(5, 2), TestRulesets.Default.Map.DefaultTerrain);
        var walking = Execute(Execute(cleared, P1, new EndTurnCommand()).State, P2, new EndTurnCommand()).State;

        blocked.FindDeal(deal)!.Paused.ShouldBeTrue();
        blocked.FindUnit(scenario.Trader)!.Position.ShouldBe(At(7, 2));
        notSwapped.Events.OfType<TradeExchangedEvent>().ShouldBeEmpty();
        walking.FindDeal(deal)!.Paused.ShouldBeFalse();
        walking.FindUnit(scenario.Trader)!.Position.ShouldBe(At(4, 2));
    }

    [Fact]
    public void Reconcile_TraderKilled_BreaksTheDeal()
    {
        // Alone on a tile, a trader is a target like any unit.
        var scenario = new TradeScenario();
        var killer = scenario.Builder.AddUnit(P2, TestRulesets.Soldier, 6, 3);
        var (active, deal) = scenario.Active();
        var state = ChangeUnit(active, scenario.Trader, static trader => trader with { Position = At(6, 2), Damage = TestRulesets.Default.Combat.MaxHealth - 1 });

        var update = Execute(state, P2, new AttackCommand(killer, At(6, 2)));

        update.State.FindUnit(scenario.Trader).ShouldBeNull();
        update.State.TradeDealList.ShouldBeEmpty();
        update.Events.OfType<TradeDealChangedEvent>().ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            changed => changed.DealId.ShouldBe(deal),
            static changed => changed.Change.ShouldBe(TradeChange.Broken));
    }

    [Fact]
    public void Reconcile_HostChangedHands_EndsTheDealAndFreesTheTrader()
    {
        var scenario = new TradeScenario();
        var (state, deal) = scenario.Active();
        var captured = state with { Cities = [.. state.Cities.Select(city => city.Id == scenario.Host ? city with { Owner = P1 } : city)] };

        var update = Execute(captured, P2, new EndTurnCommand());

        update.State.TradeDealList.ShouldBeEmpty();
        update.State.FindUnit(scenario.Trader)!.DealId.ShouldBeNull();
        update.Events.OfType<TradeDealChangedEvent>().ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            changed => changed.DealId.ShouldBe(deal),
            static changed => changed.Change.ShouldBe(TradeChange.Ended));
    }

    [Fact]
    public void Reconcile_HomeLost_MovesTheTraderToItsOwnersNearestCityWithAPost()
    {
        // P1's city at (2, 6) is nearer but has no post; the host city at (8, 2) has one, so it wins.
        var scenario = new TradeScenario(hostOwner: P1);
        scenario.Builder.AddCity(P1, 2, 6, paid: true);
        var state = scenario.Builder.Build();
        var lost = state with { Cities = [.. state.Cities.Select(city => city.Id == scenario.Home ? city with { Owner = P2 } : city)] };

        var update = Execute(lost, P1, new EndTurnCommand());

        update.State.FindUnit(scenario.Trader)!.HomeCity.ShouldBe(scenario.Host);
    }

    [Fact]
    public void UpdateDemand_UnmetBuyAndUnsoldSell_MoveTheLevelsAtTheEndOfTheOwnersTurn()
    {
        var scenario = new TradeScenario();

        var update = Execute(scenario.Builder.Build(), P1, new EndTurnCommand());

        // Need 4 Stone: 100 + 100 × 20 × 4 / (100 × (4 + 5)) = 108; offer 6 Food: 100 − 100 × 20 × 6 / (100 × 11) = 90 (truncated).
        update.State.FindCity(scenario.Home)!.DemandList.ShouldBe([new ResourceDemand(TestRulesets.Food, 90), new ResourceDemand(TestRulesets.Stone, 108)]);
        update.State.FindCity(scenario.Host)!.DemandList.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateDemand_CapsAllTaken_KeepTheLevels()
    {
        var scenario = new TradeScenario(hostOwner: P1);
        scenario.Builder.WithCity(scenario.Home, static city => city with { Market = [Order(TestRulesets.Food, MarketSide.Sell, 3), Order(TestRulesets.Stone, MarketSide.Buy, 1)] });
        var (state, _) = scenario.Proposed();
        var before = state.FindCity(scenario.Home)!.DemandList;
        state.FindDeal(state.TradeDealList.Single().Id)!.IsAgreed.ShouldBeTrue();

        var update = Execute(state, P1, new EndTurnCommand());

        update.State.FindCity(scenario.Home)!.DemandList.ShouldBe(before);
    }

    [Fact]
    public void UpdateDemand_UnpaidUpkeep_RaisesTheNeed()
    {
        // A city on desert with an empty stock can't feed the soldier on its territory.
        var builder = new GameStateBuilder().WithTerrain(TestRulesets.Desert, (2, 2), (3, 2), (1, 2), (1, 1), (2, 1), (1, 3), (2, 3));
        var city = builder.AddCity(P1, 2, 2, paid: true, buildings: TestRulesets.TradePost);
        builder.WithBuilding(TestRulesets.TradePost, (3, 2)).AddCity(P2, 10, 8);
        builder.AddUnit(P1, TestRulesets.Soldier, 1, 2);

        var update = Execute(builder.Build(), P1, new EndTurnCommand());

        // Need 1 Food: 100 + 100 × 20 × 1 / (100 × 6) = 103.
        update.State.FindCity(city)!.DemandList.ShouldBe([new ResourceDemand(TestRulesets.Food, 103)]);
    }

    [Fact]
    public void UpdateDemand_CityWithoutATradePost_HasNoLevels()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, paid: true);
        builder.AddCity(P2, 10, 8);
        builder.WithCity(city, static city => city with { Market = [Order(TestRulesets.Gold, MarketSide.Buy, 5)] });

        var update = Execute(builder.Build(), P1, new EndTurnCommand());

        update.State.FindCity(city)!.Demand.ShouldBeNull();
    }

    private static GameState WithTile(GameState state, HexCoord coord, TerrainType terrain) =>
        state with { Map = state.Map with { Tiles = [.. state.Map.Tiles.Select(tile => tile.Coord == coord ? tile with { Terrain = terrain } : tile)] } };

    [Fact]
    public void Resign_HostLeavesAGameThatGoesOn_EndsTheDealsItHosted()
    {
        var scenario = new TradeScenario();
        var (active, deal) = scenario.Active();
        var settler = new Unit(new UnitId(999), P3, TestRulesets.Settler, At(5, 9), 0);
        var state = active with { Players = [.. active.Players, new Player(P3, PlayerStatus.Active, [])], Units = [.. active.Units, settler] };

        var update = Execute(state, P2, new ResignCommand());

        update.State.FindDeal(deal).ShouldBeNull();
        update.State.FindCity(scenario.Host)!.Owner.ShouldBe(P2);
        update.Events.OfType<TradeDealChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Ended);
        update.State.FindUnit(scenario.Trader)!.DealId.ShouldBeNull();
    }
}
