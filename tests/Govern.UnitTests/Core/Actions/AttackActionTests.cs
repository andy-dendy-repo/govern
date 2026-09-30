using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class AttackActionTests
{
    [Fact]
    public void Execute_AdjacentEnemyUnit_DestroysItAndEndsAttackersMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(defender).ShouldBeNull();
        var after = update.State.FindUnit(attacker)!;
        after.Position.ShouldBe(At(3, 3));
        after.MovementLeft.ShouldBe(0);
        update.Events.ShouldContain(new UnitDestroyedEvent(defender, P2, At(4, 3)));
    }

    [Fact]
    public void Execute_UndefendedEnemyCity_CapturesItAndMovesIn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var city = builder.AddCity(P2, 4, 3, progress: 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        var captured = update.State.FindCity(city)!;
        captured.Owner.ShouldBe(P1);
        captured.ProductionProgress.ShouldBe(0);
        update.State.FindUnit(attacker)!.Position.ShouldBe(At(4, 3));
        update.Events.ShouldContain(new CityCapturedEvent(city, P2, P1, attacker));
    }

    [Fact]
    public void Execute_DefendedCity_DestroysTheDefenderButKeepsTheCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var city = builder.AddCity(P2, 4, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(defender).ShouldBeNull();
        update.State.FindCity(city)!.Owner.ShouldBe(P2);
        update.State.FindUnit(attacker)!.Position.ShouldBe(At(3, 3));
    }

    [Fact]
    public void Execute_TargetTwoTilesAway_ReturnsNotAdjacent()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 5, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(5, 3)));

        result.ShouldFailWith(RuleViolations.NotAdjacent);
    }

    [Fact]
    public void Execute_Settler_ReturnsCannotAttack()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(settler, At(4, 3)));

        result.ShouldFailWith(RuleViolations.CannotAttack);
    }

    [Fact]
    public void Execute_OwnUnit_ReturnsOwnTarget()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P1, TestRulesets.Settler, 4, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3)));

        result.ShouldFailWith(RuleViolations.OwnTarget);
    }

    [Fact]
    public void Execute_OwnCity_ReturnsOwnTarget()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 1, 0);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(0, 0)));

        result.ShouldFailWith(RuleViolations.OwnTarget);
    }

    [Fact]
    public void Execute_EmptyTile_ReturnsNoTarget()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3)));

        result.ShouldFailWith(RuleViolations.NoTarget);
    }

    [Fact]
    public void Execute_NoMovementLeft_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3, movementLeft: 0);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3)));

        result.ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_CaptureWhenTheRulesetKeepsProgress_KeepsIt()
    {
        var ruleset = TestRulesets.With(json => json["cities"]!["captureResetsProduction"] = false);
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var city = builder.AddCity(P2, 4, 3, progress: 2);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindCity(city)!.ProductionProgress.ShouldBe(2);
    }
}
