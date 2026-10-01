using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// The forecast the client shows before an attack: strengths, modifiers and damage ranges, which Execute must always respect.
public class CombatForecastTests
{
    private const int Seeds = 1000;

    public static TheoryData<string> Fights => new(
        "equal soldiers",
        "spearman defends against horseman",
        "horseman attacks archer",
        "soldier attacks spearman",
        "defender in forest",
        "defender on hills",
        "hurt attacker",
        "hurt defender",
        "archer shoots",
        "settler",
        "defender nearly dead",
        "defender across a river",
        "defender across a deep river in forest",
        "archer shoots across a river");

    // Over many seeds, every rolled damage lies in the forecast's range, and both ends of the range occur.
    [Theory]
    [MemberData(nameof(Fights))]
    public void Forecast_OverManySeeds_MatchesTheDamageExecuteRolls(string fight)
    {
        var (state, attacker, target) = Build(fight);
        var forecast = GameEngine.CombatForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), attacker, target)!;
        forecast.ShouldNotBeNull();

        var hits = Enumerable.Range(0, Seeds)
            .Select(seed => GameEngine.Execute(TestRulesets.Default, state with { RngState = (ulong)seed }, P1, new AttackCommand(attacker, target))
                .ShouldSucceed().Events.OfType<UnitAttackedEvent>().Single())
            .ToList();

        hits.Min(static hit => hit.DamageDealt).ShouldBe(forecast.Defender.MinDamage);
        hits.Max(static hit => hit.DamageDealt).ShouldBe(forecast.Defender.MaxDamage);
        hits.Min(static hit => hit.DamageTaken).ShouldBe(forecast.Attacker.MinDamage);
        hits.Max(static hit => hit.DamageTaken).ShouldBe(forecast.Attacker.MaxDamage);
    }

    [Fact]
    public void Forecast_EqualSoldiersOnPlains_Is24To36BothWays()
    {
        var forecast = Forecast("equal soldiers");

        forecast.IsRanged.ShouldBeFalse();
        forecast.Attacker.ShouldBe(new CombatSide(20, 20, [], 100, 24, 36), CombatSideComparer.Instance);
        forecast.Defender.ShouldBe(new CombatSide(20, 20, [], 100, 24, 36), CombatSideComparer.Instance);
    }

    [Fact]
    public void Forecast_SpearmanDefendingAgainstHorseman_GetsTheCounterBonus()
    {
        var forecast = Forecast("spearman defends against horseman");

        forecast.Defender.Strength.ShouldBe(28);
        forecast.Defender.Modifiers.ShouldBe([new CombatModifier(CombatModifierKind.Counter, 10, "cavalry")]);
        forecast.Attacker.Strength.ShouldBe(20);
        forecast.Attacker.Modifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Forecast_HorsemanAttackingArcher_GetsTheCounterBonus()
    {
        var forecast = Forecast("horseman attacks archer");

        forecast.Attacker.Modifiers.ShouldBe([new CombatModifier(CombatModifierKind.Counter, 5, "ranged")]);
        forecast.Attacker.Strength.ShouldBe(25);
        forecast.Defender.Strength.ShouldBe(10);
    }

    [Fact]
    public void Forecast_DefenderInForest_GetsTheTerrainBonusButNotTheAttacker()
    {
        var forecast = Forecast("defender in forest");

        forecast.Defender.Modifiers.ShouldBe([new CombatModifier(CombatModifierKind.Terrain, 3, "forest")]);
        forecast.Defender.Strength.ShouldBe(23);
        forecast.Attacker.Modifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Forecast_MeleeAcrossARiver_GivesTheDefenderTheRiverBonus()
    {
        var forecast = Forecast("defender across a river");

        forecast.Defender.Modifiers.ShouldBe([new CombatModifier(CombatModifierKind.River, 3, "river")]);
        forecast.Defender.Strength.ShouldBe(23);
        forecast.Attacker.Modifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Forecast_MeleeAcrossADeepRiverIntoForest_AddsBothBonuses()
    {
        var forecast = Forecast("defender across a deep river in forest");

        forecast.Defender.Modifiers.ShouldBe([
            new CombatModifier(CombatModifierKind.Terrain, 3, "forest"),
            new CombatModifier(CombatModifierKind.River, 5, "deep_river"),
        ]);
        forecast.Defender.Strength.ShouldBe(28);
    }

    [Fact]
    public void Forecast_MeleeOverABridge_GivesNoRiverBonus()
    {
        var forecast = Forecast("defender across a bridged deep river");

        forecast.Defender.Modifiers.ShouldBeEmpty();
        forecast.Defender.Strength.ShouldBe(20);
    }

    [Fact]
    public void Forecast_RangedAcrossARiver_GivesNoRiverBonus()
    {
        var forecast = Forecast("archer shoots across a river");

        forecast.Defender.Modifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Forecast_HurtUnit_LosesUpToHalfItsStrength()
    {
        var forecast = Forecast("hurt attacker");

        // Health 50 of 100: 20 × 150 / 200 = 15.
        forecast.Attacker.Health.ShouldBe(50);
        forecast.Attacker.Modifiers.ShouldBe([new CombatModifier(CombatModifierKind.Health, -5, null)]);
        forecast.Attacker.Strength.ShouldBe(15);
    }

    [Fact]
    public void Forecast_Archer_TakesNoDamage()
    {
        var forecast = Forecast("archer shoots");

        forecast.IsRanged.ShouldBeTrue();
        forecast.Attacker.BaseStrength.ShouldBe(22);
        (forecast.Attacker.MinDamage, forecast.Attacker.MaxDamage).ShouldBe((0, 0));
        forecast.Attacker.MayBeDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Forecast_Settler_IsSureToBeDestroyed()
    {
        var forecast = Forecast("settler");

        forecast.Defender.IsDestroyed.ShouldBeTrue();
        forecast.Defender.Strength.ShouldBe(0);
        (forecast.Attacker.MinDamage, forecast.Attacker.MaxDamage).ShouldBe((0, 0));
    }

    [Fact]
    public void Forecast_DefenderNearlyDead_MayBeDestroyed()
    {
        var forecast = Forecast("defender nearly dead");

        forecast.Defender.MaxDamage.ShouldBe(forecast.Defender.Health);
        forecast.Defender.MayBeDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void Forecast_UndefendedCityEmptyTileOrOwnUnit_IsNull()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddCity(P2, 4, 3);
        builder.AddUnit(P1, TestRulesets.Settler, 2, 3);
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        GameEngine.CombatForecast(TestRulesets.Default, view, soldier, At(4, 3)).ShouldBeNull();
        GameEngine.CombatForecast(TestRulesets.Default, view, soldier, At(3, 4)).ShouldBeNull();
        GameEngine.CombatForecast(TestRulesets.Default, view, soldier, At(2, 3)).ShouldBeNull();
    }

    [Fact]
    public void Forecast_NotYourTurn_IsNull()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);

        GameEngine.CombatForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1), soldier, At(4, 3)).ShouldBeNull();
    }

    [Fact]
    public void Forecast_NullView_Throws() =>
        Should.Throw<ArgumentNullException>(() => GameEngine.CombatForecast(TestRulesets.Default, null!, new UnitId(1), At(0, 0)));

    private static CombatForecast Forecast(string fight)
    {
        var (state, attacker, target) = Build(fight);
        return GameEngine.CombatForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), attacker, target)!;
    }

    private static (GameState State, UnitId Attacker, HexCoord Target) Build(string fight)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var (attackerType, defenderType, attackerDamage, defenderDamage) = fight switch
        {
            "spearman defends against horseman" => (TestRulesets.Horseman, TestRulesets.Spearman, 0, 0),
            "horseman attacks archer" => (TestRulesets.Horseman, TestRulesets.Archer, 0, 0),
            "soldier attacks spearman" => (TestRulesets.Soldier, TestRulesets.Spearman, 0, 0),
            "hurt attacker" => (TestRulesets.Soldier, TestRulesets.Soldier, 50, 0),
            "hurt defender" => (TestRulesets.Soldier, TestRulesets.Soldier, 0, 60),
            "archer shoots" or "archer shoots across a river" => (TestRulesets.Archer, TestRulesets.Soldier, 0, 0),
            "settler" => (TestRulesets.Soldier, TestRulesets.Settler, 0, 0),
            "defender nearly dead" => (TestRulesets.Soldier, TestRulesets.Soldier, 0, 70),
            _ => (TestRulesets.Soldier, TestRulesets.Soldier, 0, 0),
        };

        var attacker = builder.AddUnit(P1, attackerType, 3, 3, damage: attackerDamage);
        var targetColumn = fight is "archer shoots" or "archer shoots across a river" ? 5 : 4;
        builder.AddUnit(P2, defenderType, targetColumn, 3, damage: defenderDamage);
        _ = fight switch
        {
            "defender in forest" => builder.WithTerrain(TestRulesets.Forest, (4, 3)),
            "defender on hills" => builder.WithTerrain(TestRulesets.Hills, (4, 3)).WithTerrain(TestRulesets.Forest, (3, 3)),
            "defender across a river" => builder.WithRiver(TestRulesets.River, ((3, 3), (4, 3))),
            "defender across a deep river in forest" => builder.WithRiver(TestRulesets.DeepRiver, ((3, 3), (4, 3))).WithTerrain(TestRulesets.Forest, (4, 3)),
            "archer shoots across a river" => builder.WithRiver(TestRulesets.River, ((4, 3), (5, 3))),
            "defender across a bridged deep river" => builder.WithRiver(TestRulesets.DeepRiver, ((3, 3), (4, 3))).WithBridge(((4, 3), (3, 3))),
            _ => builder,
        };

        return (builder.Build(), attacker, At(targetColumn, 3));
    }

    // CombatSide holds an ImmutableArray, which records don't compare by content.
    private sealed class CombatSideComparer : IEqualityComparer<CombatSide>
    {
        public static readonly CombatSideComparer Instance = new();

        public bool Equals(CombatSide? x, CombatSide? y) =>
            x is not null && y is not null
            && x.Modifiers.SequenceEqual(y.Modifiers)
            && x with { Modifiers = [] } == y with { Modifiers = [] };

        public int GetHashCode(CombatSide obj) => obj.Strength;
    }
}
