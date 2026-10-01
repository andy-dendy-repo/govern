using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class AttackActionTests
{
    [Fact]
    public void Execute_EqualSoldiers_BothTakeDamageAndTheAttackerStops()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        // Strength 20 against 20: 30 damage each, rolled within 80-120 %.
        var hit = update.Events.OfType<UnitAttackedEvent>().ShouldHaveSingleItem();
        hit.DamageDealt.ShouldBeInRange(24, 36);
        hit.DamageTaken.ShouldBeInRange(24, 36);
        update.State.FindUnit(defender)!.Damage.ShouldBe(hit.DamageDealt);
        var after = update.State.FindUnit(attacker)!;
        after.Damage.ShouldBe(hit.DamageTaken);
        after.Position.ShouldBe(At(3, 3));
        after.MovementLeft.ShouldBe(0);
        update.Events.OfType<UnitDestroyedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Execute_DamageRolls_AreHashedNotRawGeneratorDraws()
    {
        // Damage is shown to both players, and raw SplitMix64 draws are invertible: a raw roll would let a player rebuild the
        // generator state. Strength 20 against 20 deals 30 × roll percent (80-120), defender first, then the attacker.
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);
        var rawDiffers = false;

        foreach (var rngState in new ulong[] { 1, 42, 7_777, 123_456_789 })
        {
            var hashed = new DeterministicRandom(rngState);
            var raw = new DeterministicRandom(rngState);
            (int Dealt, int Taken) expected = (30 * hashed.NextHashed(80, 121) / 100, 30 * hashed.NextHashed(80, 121) / 100);
            (int Dealt, int Taken) rawRolls = (30 * raw.Next(80, 121) / 100, 30 * raw.Next(80, 121) / 100);

            var state = builder.Build() with { RngState = rngState };
            var hit = GameEngine.Execute(TestRulesets.Default, state, P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed()
                .Events.OfType<UnitAttackedEvent>().Single();

            (hit.DamageDealt, hit.DamageTaken).ShouldBe(expected);
            rawDiffers |= rawRolls != expected;
        }

        rawDiffers.ShouldBeTrue("the seeds must tell hashed and raw rolls apart");
    }

    [Fact]
    public void Execute_DefenderAtLowHealth_IsDestroyed()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3, damage: 95);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(defender).ShouldBeNull();
        update.Events.ShouldContain(new UnitDestroyedEvent(defender, P2, At(4, 3)));
        update.Events.OfType<UnitAttackedEvent>().Single().DamageDealt.ShouldBe(5);
        update.State.FindUnit(attacker)!.Position.ShouldBe(At(3, 3));
    }

    [Fact]
    public void Execute_HurtAttacker_IsDestroyedByTheDamageBack()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3, damage: 95);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(attacker).ShouldBeNull();
        update.State.FindUnit(defender).ShouldNotBeNull();
        update.Events.ShouldContain(new UnitDestroyedEvent(attacker, P1, At(3, 3)));
    }

    [Fact]
    public void Execute_BothNearlyDead_DestroysBoth()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3, damage: 95);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3, damage: 95);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(attacker).ShouldBeNull();
        update.State.FindUnit(defender).ShouldBeNull();
        update.Events.OfType<UnitDestroyedEvent>().Count().ShouldBe(2);
    }

    [Fact]
    public void Execute_ArcherTwoTilesAway_DamagesWithoutDamageBack()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var archer = builder.AddUnit(P1, TestRulesets.Archer, 3, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 5, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(archer, At(5, 3))).ShouldSucceed();

        // Ranged strength 22 against 20: 34 damage, rolled within 80-120 %.
        var hit = update.Events.OfType<UnitAttackedEvent>().Single();
        hit.DamageDealt.ShouldBeInRange(27, 40);
        hit.DamageTaken.ShouldBe(0);
        update.State.FindUnit(defender)!.Damage.ShouldBe(hit.DamageDealt);
        update.State.FindUnit(archer)!.ShouldSatisfyAllConditions(
            unit => unit.Damage.ShouldBe(0),
            unit => unit.Position.ShouldBe(At(3, 3)),
            unit => unit.MovementLeft.ShouldBe(0));
    }

    [Fact]
    public void Execute_ArcherThreeTilesAway_ReturnsOutOfRange()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var archer = builder.AddUnit(P1, TestRulesets.Archer, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 6, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(archer, At(6, 3)));

        result.ShouldFailWith(RuleViolations.OutOfRange);
    }

    [Fact]
    public void Execute_ArcherOnUndefendedCity_ReturnsNoTarget()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var archer = builder.AddUnit(P1, TestRulesets.Archer, 3, 3);
        builder.AddCity(P2, 4, 3);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(archer, At(4, 3)));

        result.ShouldFailWith(RuleViolations.NoTarget);
    }

    [Fact]
    public void Execute_Civilian_IsDestroyedAndDoesNoDamageBack()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var settler = builder.AddUnit(P2, TestRulesets.Settler, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(settler).ShouldBeNull();
        update.State.FindUnit(attacker)!.Damage.ShouldBe(0);
        update.Events.ShouldContain(new UnitAttackedEvent(attacker, P1, At(3, 3), settler, P2, At(4, 3), 100, 0));
    }

    [Fact]
    public void Execute_SameStateTwice_GivesTheSameFight()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);
        var state = builder.Build();

        var first = GameEngine.Execute(TestRulesets.Default, state, P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();
        var second = GameEngine.Execute(TestRulesets.Default, state, P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        GameSerializer.Serialize(second.State).ShouldBe(GameSerializer.Serialize(first.State));
        first.State.RngState.ShouldNotBe(state.RngState);
    }

    [Fact]
    public void Execute_DifferentSeeds_RollDifferentDamage()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);
        var state = builder.Build();

        var dealt = Enumerable.Range(1, 50)
            .Select(seed => GameEngine.Execute(TestRulesets.Default, state with { RngState = (ulong)seed }, P1, new AttackCommand(attacker, At(4, 3)))
                .ShouldSucceed().Events.OfType<UnitAttackedEvent>().Single().DamageDealt)
            .ToList();

        dealt.ShouldAllBe(damage => damage >= 24 && damage <= 36);
        dealt.Distinct().Count().ShouldBeGreaterThan(5);
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
    public void Execute_DefendedCity_FightsTheDefenderAndKeepsTheCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var city = builder.AddCity(P2, 4, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.FindUnit(defender)!.Damage.ShouldBeGreaterThan(0);
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
