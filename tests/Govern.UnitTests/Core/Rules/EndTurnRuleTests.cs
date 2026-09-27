using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Rules;

public class EndTurnRuleTests
{
    [Fact]
    public void Execute_FirstPlayerEnds_PassesToSecondPlayerInSameRound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.CurrentPlayer.ShouldBe(P2);
        update.State.Turn.ShouldBe(1);
        update.Events.ShouldContain(new TurnEndedEvent(P1, P2, 1));
    }

    [Fact]
    public void Execute_LastPlayerEnds_StartsNextRound()
    {
        var state = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2, turn: 3).Build();

        var update = GameEngine.Execute(state, P2, new EndTurnCommand()).ShouldSucceed();

        update.State.CurrentPlayer.ShouldBe(P1);
        update.State.Turn.ShouldBe(4);
    }

    [Fact]
    public void Execute_EndTurn_RestoresOnlyTheNextPlayersMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, UnitType.Soldier, 2, 2, movementLeft: 0);
        var enemy = builder.AddUnit(P2, UnitType.Soldier, 8, 2, movementLeft: 0);

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindUnit(own)!.MovementLeft.ShouldBe(0);
        update.State.FindUnit(enemy)!.MovementLeft.ShouldBe(UnitCatalog.Get(UnitType.Soldier).Movement);
    }

    [Fact]
    public void Execute_EndTurn_AddsProgressToTheEndingPlayersCitiesOnly()
    {
        var builder = new GameStateBuilder();
        var own = builder.AddCity(P1, 2, 2, progress: 1);
        var enemy = builder.AddCity(P2, 8, 8, progress: 1);

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindCity(own)!.ProductionProgress.ShouldBe(2);
        update.State.FindCity(enemy)!.ProductionProgress.ShouldBe(1);
    }

    [Fact]
    public void Execute_ProductionComplete_SpawnsUnitOnCityWithoutMovement()
    {
        var builder = new GameStateBuilder();
        var cost = UnitCatalog.Get(UnitType.Soldier).ProductionCost;
        var city = builder.AddCity(P1, 2, 2, UnitType.Soldier, progress: cost - 1);
        builder.AddCity(P2, 8, 8);

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

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
        var city = builder.AddCity(P1, 2, 2, UnitType.Settler, progress: UnitCatalog.Get(UnitType.Settler).ProductionCost - 1);
        builder.AddCity(P2, 8, 8);

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        var created = update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        created.Unit.Type.ShouldBe(UnitType.Settler);
        created.CityId.ShouldBe(city);
    }

    [Fact]
    public void Execute_ProgressAboveCheaperNewTarget_SpawnsImmediately()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, UnitType.Settler, progress: 5);
        builder.AddCity(P2, 8, 8);
        var switched = GameEngine.Execute(builder.Build(), P1, new SetProductionCommand(city, UnitType.Soldier)).ShouldSucceed().State;

        var update = GameEngine.Execute(switched, P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().Unit.Type.ShouldBe(UnitType.Soldier);
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(0);
    }

    [Fact]
    public void Execute_CityTileOccupied_SpawnsOnFreeNeighbour()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 4, 4, UnitType.Soldier, progress: UnitCatalog.Get(UnitType.Soldier).ProductionCost - 1);
        builder.AddCity(P2, 10, 8);
        builder.AddUnit(P1, UnitType.Soldier, 4, 4);

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        var created = update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        created.CityId.ShouldBe(city);
        created.Unit.Position.DistanceTo(At(4, 4)).ShouldBe(1);
    }

    [Fact]
    public void Execute_CitySurrounded_WaitsWithFullProgress()
    {
        var builder = new GameStateBuilder();
        var cost = UnitCatalog.Get(UnitType.Soldier).ProductionCost;
        var city = builder.AddCity(P1, 4, 4, UnitType.Soldier, progress: cost - 1);
        builder.AddCity(P2, 10, 8);
        foreach (var coord in At(4, 4).WithinRadius(1))
        {
            builder.AddUnit(P1, UnitType.Soldier, coord.Column, coord.Row);
        }

        var update = GameEngine.Execute(builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldBeEmpty();
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(cost);
    }
}
