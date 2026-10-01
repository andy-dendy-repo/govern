using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class EndTurnActionTests
{
    [Fact]
    public void Execute_FirstPlayerEnds_PassesToSecondPlayerInSameRound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.CurrentPlayer.ShouldBe(P2);
        update.State.Turn.ShouldBe(1);
        update.Events.ShouldContain(new TurnEndedEvent(P1, P2, 1));
    }

    [Fact]
    public void Execute_LastPlayerEnds_StartsNextRound()
    {
        var state = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2, turn: 3).Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P2, new EndTurnCommand()).ShouldSucceed();

        update.State.CurrentPlayer.ShouldBe(P1);
        update.State.Turn.ShouldBe(4);
    }

    [Fact]
    public void Execute_EndTurn_RestoresOnlyTheNextPlayersMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 0);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 8, 2, movementLeft: 0);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(own)!.MovementLeft.ShouldBe(0);
        update.State.FindUnit(enemy)!.MovementLeft.ShouldBe(TestRulesets.Default.Unit(TestRulesets.Soldier).Movement);
    }

    [Fact]
    public void Execute_EndTurn_AddsProgressToTheEndingPlayersCitiesOnly()
    {
        var builder = new GameStateBuilder();
        var own = builder.AddCity(P1, 2, 2, progress: 1);
        var enemy = builder.AddCity(P2, 8, 8, progress: 1);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindCity(own)!.ProductionProgress.ShouldBe(2);
        update.State.FindCity(enemy)!.ProductionProgress.ShouldBe(1);
    }

    [Fact]
    public void Execute_ProductionComplete_SpawnsUnitOnCityWithoutMovement()
    {
        var builder = new GameStateBuilder();
        var cost = TestRulesets.Default.Unit(TestRulesets.Soldier).ProductionCost;
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Soldier, progress: cost - 1);
        builder.AddCity(P2, 8, 8);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        var created = update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        created.CityId.ShouldBe(city);
        created.Unit.Position.ShouldBe(At(2, 2));
        created.Unit.MovementLeft.ShouldBe(0);
        update.State.UnitAt(At(2, 2)).ShouldBe(created.Unit);
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(0);
    }

    [Fact]
    public void Execute_SettlerProductionComplete_SpawnsSettler()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Settler, progress: TestRulesets.Default.Unit(TestRulesets.Settler).ProductionCost - 1);
        builder.AddCity(P2, 8, 8);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        var created = update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        created.Unit.Type.ShouldBe(TestRulesets.Settler);
        created.CityId.ShouldBe(city);
    }

    [Fact]
    public void Execute_ProgressAboveCheaperNewTarget_SpawnsImmediately()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Settler, progress: 5);
        builder.AddCity(P2, 8, 8);
        var switched = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Soldier)).ShouldSucceed().State;

        var update = GameEngine.Execute(TestRulesets.Default, switched, P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().Unit.Type.ShouldBe(TestRulesets.Soldier);
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(0);
    }

    [Fact]
    public void Execute_CityTileOccupied_SpawnsOnFreeNeighbour()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 4, 4, TestRulesets.Soldier, progress: TestRulesets.Default.Unit(TestRulesets.Soldier).ProductionCost - 1);
        builder.AddCity(P2, 10, 8);
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        var created = update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        created.CityId.ShouldBe(city);
        created.Unit.Position.DistanceTo(At(4, 4)).ShouldBe(1);
    }

    [Fact]
    public void Execute_CitySurrounded_WaitsWithFullProgress()
    {
        var builder = new GameStateBuilder();
        var cost = TestRulesets.Default.Unit(TestRulesets.Soldier).ProductionCost;
        var city = builder.AddCity(P1, 4, 4, TestRulesets.Soldier, progress: cost - 1);
        builder.AddCity(P2, 10, 8);
        foreach (var coord in At(4, 4).WithinRadius(1))
        {
            builder.AddUnit(P1, TestRulesets.Soldier, coord.Column, coord.Row);
        }

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldBeEmpty();
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(cost);
    }

    [Fact]
    public void Execute_RulesetProductionPerTurnAndCost_AreUsed()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["cities"]!["productionPerTurn"] = 3;
            json["units"]![1]!["productionCost"] = 7;
        });
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Soldier, progress: 3);
        builder.AddCity(P2, 8, 8);

        var first = GameEngine.Execute(ruleset, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        first.State.FindCity(city)!.ProductionProgress.ShouldBe(6);
        first.Events.OfType<UnitCreatedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Execute_NextPlayersTurn_RestoresTheirMovementFromTheRuleset()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["units"]![1]!["movement"] = 3;
            json["units"]![1]!["sight"] = 3;
        });
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 6, 6, movementLeft: 0);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(enemy)!.MovementLeft.ShouldBe(3);
    }

    [Fact]
    public void Execute_HurtUnitThatRested_Heals()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var rested = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, damage: 40);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(rested)!.Damage.ShouldBe(30);
    }

    [Fact]
    public void Execute_HurtUnitRestingInOwnCity_HealsMore()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var rested = builder.AddUnit(P1, TestRulesets.Soldier, 0, 0, damage: 40);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(rested)!.Damage.ShouldBe(20);
    }

    [Fact]
    public void Execute_NearlyHealedUnit_HealsOnlyToFullHealth()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var rested = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, damage: 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(rested)!.Damage.ShouldBe(0);
    }

    [Fact]
    public void Execute_UnitThatMovedOrAttacked_DoesNotHeal()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var moved = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, movementLeft: 1, damage: 40);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 8, 4, damage: 40);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(moved)!.Damage.ShouldBe(40);
        update.State.FindUnit(enemy)!.Damage.ShouldBe(40, "only the player ending the turn heals");
    }
}
