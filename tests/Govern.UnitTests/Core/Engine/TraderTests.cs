using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;
using static Govern.UnitTests.Core.Builders.TradeScenario;

namespace Govern.UnitTests.Core.Engine;

// Traders (G17b): a home city each, at most trade.tradersPerCity per city, tied to their deal, and sharing tiles with anything.
public class TraderTests
{
    [Fact]
    public void SetProduction_CityWithAllItsTraders_IsRejected()
    {
        var scenario = new TradeScenario();
        scenario.Builder.WithNodes(P1, "trade").AddUnit(P1, TestRulesets.Trader, 2, 3, homeCity: scenario.Home);
        var oneTrader = scenario.Builder.Build();
        var oneLess = oneTrader with { Units = [.. oneTrader.Units.Where(unit => unit.Id != scenario.Trader)] };

        GameEngine.Execute(TestRulesets.Default, oneLess, P1, new SetProductionCommand(scenario.Home, TestRulesets.Trader)).ShouldSucceed();
        GameEngine.Execute(TestRulesets.Default, scenario.Builder.Build(), P1, new SetProductionCommand(scenario.Home, TestRulesets.Trader))
            .ShouldFailWith(RuleViolations.TooManyTraders);
    }

    [Fact]
    public void EndTurn_FinishedTraderOverTheLimit_WaitsInProduction()
    {
        var builder = new GameStateBuilder();
        var cost = TestRulesets.Default.Unit(TestRulesets.Trader).ProductionCost;
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Trader, progress: cost - 1, stock: Plenty, paid: true);
        builder.AddCity(P2, 10, 8);
        var state = builder.Build();
        var full = TestRulesets.Default.Trade.TradersPerCity;
        var crowded = state with
        {
            Units = [.. state.Units, .. Enumerable.Range(0, full).Select(index => new Unit(new UnitId(900 + index), P1, TestRulesets.Trader, At(3, 3), 0, HomeCity: city))],
        };

        var waits = Execute(crowded, P1, new EndTurnCommand());
        var made = Execute(state, P1, new EndTurnCommand());

        waits.Events.OfType<UnitCreatedEvent>().ShouldBeEmpty();
        waits.State.FindCity(city)!.ProductionProgress.ShouldBe(cost);
        made.State.Units.ShouldContain(unit => unit.Type == TestRulesets.Trader && unit.HomeCity == city);
    }

    [Fact]
    public void Move_TraderOnADeal_IsRejected()
    {
        var scenario = new TradeScenario();
        var (accepted, _) = scenario.Accepted();
        var p1Turn = Execute(accepted, P2, new EndTurnCommand()).State;

        GameEngine.Execute(TestRulesets.Default, p1Turn, P1, new MoveUnitCommand(scenario.Trader, At(5, 3))).ShouldFailWith(RuleViolations.OnTradeDeal);
    }

    [Fact]
    public void Move_OntoAndThroughTraders_IsAllowedBothWays()
    {
        // A P2 trader stands at (4, 2): P1's soldier steps onto it, and P1's trader steps onto the soldier and on through.
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 3, 2);
        var trader = builder.AddUnit(P1, TestRulesets.Trader, 2, 2);
        builder.AddUnit(P2, TestRulesets.Trader, 4, 2);
        var state = builder.Build();

        var onEnemyTrader = Execute(state, P1, new MoveUnitCommand(soldier, At(4, 2))).State;
        var shared = Execute(onEnemyTrader, P1, new MoveUnitCommand(trader, At(4, 2))).State;

        shared.UnitsAt(At(4, 2)).Count().ShouldBe(3);
        GameEngine.Execute(TestRulesets.Default, shared, P1, new MoveUnitCommand(trader, At(5, 2))).ShouldSucceed();
    }

    [Fact]
    public void Attack_TileWithASoldierAndATrader_HitsTheSoldier()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 2);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 2);
        var trader = builder.AddUnit(P2, TestRulesets.Trader, 4, 2);

        var update = Execute(builder.Build(), P1, new AttackCommand(attacker, At(4, 2)));

        update.State.FindUnit(trader)!.Damage.ShouldBe(0);
        (update.State.FindUnit(defender)?.Damage ?? TestRulesets.Default.Combat.MaxHealth).ShouldBeGreaterThan(0);
    }
}
