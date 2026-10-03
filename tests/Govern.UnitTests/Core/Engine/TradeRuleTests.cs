using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
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
        var (state, route) = scenario.Accepted();

        var update = Execute(state, P2, new EndTurnCommand());

        StockOf(update.State, scenario.Home, TestRulesets.Stone).ShouldBe(31);
        StockOf(update.State, scenario.Host, TestRulesets.Stone).ShouldBe(29);
        var exchanged = update.Events.OfType<TradeExchangedEvent>().ShouldHaveSingleItem();
        (exchanged.RouteId, exchanged.HomeShort, exchanged.HostShort).ShouldBe((route, false, false));
        exchanged.Gives.ShouldBe(Amounts((TestRulesets.Food, 3)));
    }

    [Fact]
    public void EndTurn_OwnersTurn_SwapsNothing()
    {
        var scenario = new TradeScenario();
        var (accepted, _) = scenario.Accepted();
        var p1Turn = Execute(accepted, P2, new EndTurnCommand()).State;

        var update = Execute(p1Turn, P1, new EndTurnCommand());

        update.Events.OfType<TradeExchangedEvent>().ShouldBeEmpty();
        StockOf(update.State, scenario.Home, TestRulesets.Stone).ShouldBe(31);
    }

    [Fact]
    public void EndTurn_SideWithoutItsPart_SwapsNothingThatTurn()
    {
        var scenario = new TradeScenario();
        scenario.Builder.WithCity(scenario.Host, static city => city with { Stock = Amounts((TestRulesets.Food, 30), (TestRulesets.Water, 30)) });
        var (state, _) = scenario.Accepted();

        var update = Execute(state, P2, new EndTurnCommand());

        var exchanged = update.Events.OfType<TradeExchangedEvent>().ShouldHaveSingleItem();
        (exchanged.HomeShort, exchanged.HostShort).ShouldBe((false, true));
        StockOf(update.State, scenario.Home, TestRulesets.Stone).ShouldBe(30);
        StockOf(update.State, scenario.Host, TestRulesets.Food).ShouldBe(StockOf(Execute(state with { TradeRoutes = null }, P2, new EndTurnCommand()).State, scenario.Host, TestRulesets.Food));
    }

    [Fact]
    public void Walk_ActiveDeal_ShuttlesTheTraderBetweenThePosts()
    {
        var scenario = new TradeScenario();
        var (state, _) = scenario.Accepted();

        var round3 = Execute(state, P2, new EndTurnCommand()).State;
        var round4 = EndRound(round3);

        round3.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(4, 2)),
            static trader => trader.Destination.ShouldBe(At(7, 2)));
        round4.FindUnit(scenario.Trader)!.ShouldSatisfyAllConditions(
            static trader => trader.Position.ShouldBe(At(6, 2)),
            static trader => trader.Destination.ShouldBe(At(3, 2)));
    }

    [Fact]
    public void Walk_WayBlocked_PausesTheDealUntilItIsFree()
    {
        // A wall of lakes down column 5 with one gap at (5, 2), where a P2 soldier steps in once the deal is accepted.
        var scenario = new TradeScenario();
        scenario.Builder.WithTerrain(TestRulesets.Lake, [.. Enumerable.Range(0, Height).Where(static row => row != 2).Select(static row => (5, row))]);
        var (accepted, route) = scenario.Accepted();
        var state = accepted with { Units = [.. accepted.Units, new Unit(new UnitId(500), P2, TestRulesets.Soldier, At(5, 2), 0)] };

        var blocked = Execute(state, P2, new EndTurnCommand()).State;
        var notSwapped = Execute(Execute(blocked, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());
        var cleared = notSwapped.State with { Units = [.. notSwapped.State.Units.Where(static unit => unit.Id != new UnitId(500))] };
        var walking = Execute(Execute(cleared, P1, new EndTurnCommand()).State, P2, new EndTurnCommand()).State;

        blocked.FindRoute(route)!.Paused.ShouldBeTrue();
        blocked.FindUnit(scenario.Trader)!.Position.ShouldBe(At(6, 2));
        notSwapped.Events.OfType<TradeExchangedEvent>().ShouldBeEmpty();
        walking.FindRoute(route)!.Paused.ShouldBeFalse();
        walking.FindUnit(scenario.Trader)!.Position.ShouldBe(At(4, 2));
    }

    [Fact]
    public void Reconcile_TraderKilled_BreaksTheDealAndKeepsTheRoute()
    {
        var scenario = new TradeScenario();
        var killer = scenario.Builder.AddUnit(P2, TestRulesets.Soldier, 5, 3);
        var (state, route) = scenario.Accepted();
        var nearTrader = state.FindUnit(scenario.Trader)!.Position;

        var update = Execute(state, P2, new AttackCommand(killer, nearTrader));

        update.State.FindUnit(scenario.Trader).ShouldBeNull();
        var broken = update.State.FindRoute(route).ShouldNotBeNull();
        (broken.Trader, broken.Deal).ShouldBe((null, null));
        update.Events.OfType<TradeRouteChangedEvent>().ShouldHaveSingleItem().Change.ShouldBe(TradeChange.Broken);
    }

    [Fact]
    public void Reconcile_HostChangedHands_ClosesTheRouteAndFreesTheTrader()
    {
        var scenario = new TradeScenario();
        var (state, route) = scenario.Accepted();
        var captured = state with { Cities = [.. state.Cities.Select(city => city.Id == scenario.Host ? city with { Owner = P1 } : city)] };

        var update = Execute(captured, P2, new EndTurnCommand());

        update.State.TradeRouteList.ShouldBeEmpty();
        update.State.FindUnit(scenario.Trader)!.RouteId.ShouldBeNull();
        update.Events.OfType<TradeRouteChangedEvent>().ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            changed => changed.RouteId.ShouldBe(route),
            static changed => changed.Change.ShouldBe(TradeChange.Closed));
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
}
