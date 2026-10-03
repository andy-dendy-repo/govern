using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class SetDestinationActionTests
{
    [Fact]
    public void Execute_FarTile_WalksNowAndKeepsTheOrder()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(9, 2)));

        var unit = update.State.FindUnit(soldier)!;
        unit.Position.ShouldBe(At(4, 2));
        unit.MovementLeft.ShouldBe(0);
        unit.Destination.ShouldBe(At(9, 2));
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>().Path.ShouldBe([At(3, 2), At(4, 2)]);
    }

    [Fact]
    public void EndTurn_UnitWithDestination_WalksOnAtTheStartOfItsOwnersTurnUntilItArrives()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(9, 2))).State;

        var afterP1 = Execute(state, P1, new EndTurnCommand());
        var round2 = Execute(afterP1.State, P2, new EndTurnCommand());
        var round3 = Execute(Execute(round2.State, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());
        var round4 = Execute(Execute(round3.State, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());

        afterP1.State.FindUnit(soldier)!.Position.ShouldBe(At(4, 2));
        round2.State.FindUnit(soldier)!.Position.ShouldBe(At(6, 2));
        round2.State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
        round2.Events.OfType<UnitMovedEvent>().ShouldHaveSingleItem().Path.ShouldBe([At(5, 2), At(6, 2)]);
        round3.State.FindUnit(soldier)!.Position.ShouldBe(At(8, 2));
        var arrived = round4.State.FindUnit(soldier)!;
        arrived.Position.ShouldBe(At(9, 2));
        arrived.MovementLeft.ShouldBe(1);
        arrived.Destination.ShouldBeNull();
        round4.Events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(OrderEndReason.Arrived);
    }

    [Fact]
    public void Execute_NeighbourWithinReach_ArrivesAtOnceAndEndsTheOrder()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(3, 2)));

        update.State.FindUnit(soldier)!.Destination.ShouldBeNull();
        var ended = update.Events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem();
        ended.ShouldBe(new UnitOrderEndedEvent(soldier, P1, TestRulesets.Soldier, At(3, 2), At(3, 2), OrderEndReason.Arrived));
    }

    [Fact]
    public void EndTurn_EnemyComesIntoSight_StopsTheUnitAndEndsTheOrder()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 7, 2);
        var state = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(9, 2))).State;

        var round2 = Execute(Execute(state, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());

        var stopped = round2.State.FindUnit(soldier)!;
        stopped.Position.ShouldBe(At(5, 2));
        stopped.MovementLeft.ShouldBe(1);
        stopped.Destination.ShouldBeNull();
        round2.Events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(OrderEndReason.EnemySighted);
    }

    [Fact]
    public void Execute_HiddenEnemyOnAnExploredTarget_IsAcceptedAndTheWalkEndsWhenItComesIntoSight()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 5, 2);

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(5, 2)));

        update.State.FindUnit(soldier)!.Position.ShouldBe(At(3, 2));
        update.State.FindUnit(soldier)!.Destination.ShouldBeNull();
        update.Events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(OrderEndReason.EnemySighted);
    }

    [Fact]
    public void Execute_EnemyAlreadyInSight_DoesNotStopTheWalk()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 2, 4);

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(9, 2)));

        update.State.FindUnit(soldier)!.Position.ShouldBe(At(4, 2));
        update.State.FindUnit(soldier)!.Destination.ShouldBe(At(9, 2));
        update.Events.OfType<UnitOrderEndedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void EndTurn_DestinationTakenByOwnUnit_EndsTheOrderAsBlocked()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 0, destination: (6, 2));
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 8, 2);
        var state = Execute(builder.Build(), P1, new MoveUnitCommand(settler, At(6, 2))).State;

        var round2 = Execute(Execute(state, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());

        round2.State.FindUnit(soldier)!.Position.ShouldBe(At(2, 2));
        round2.State.FindUnit(soldier)!.Destination.ShouldBeNull();
        round2.Events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(OrderEndReason.Blocked);
    }

    [Fact]
    public void Execute_NoMovementLeft_KeepsTheOrderAndDropsTheJob()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var worker = builder.AddUnit(P1, TestRulesets.Builder, 1, 1, movementLeft: 0, job: new UnitJob(1, 1, Improvement: TestRulesets.Farm));

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(worker, At(3, 2)));

        var unit = update.State.FindUnit(worker)!;
        unit.Position.ShouldBe(At(1, 1));
        unit.Job.ShouldBeNull();
        unit.Destination.ShouldBe(At(3, 2));
        update.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_NullTarget_CancelsTheOrder()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, destination: (9, 2));

        var update = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, null));

        update.State.FindUnit(soldier)!.Destination.ShouldBeNull();
        update.State.FindUnit(soldier)!.Position.ShouldBe(At(2, 2));
        update.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_OtherCommandOnTheUnit_ReplacesTheOrder()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, destination: (9, 2));

        var update = Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        update.State.FindUnit(soldier)!.Destination.ShouldBeNull();
    }

    [Fact]
    public void Execute_UnexploredTile_ReturnsNoPath()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetDestinationCommand(soldier, At(9, 6)));

        result.ShouldFailWith(RuleViolations.NoPath);
    }

    [Fact]
    public void Execute_ImpassableOrOwnTile_ReturnsNoPath()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1).WithTerrain(TestRulesets.Lake, (6, 6));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetDestinationCommand(soldier, At(6, 6))).ShouldFailWith(RuleViolations.NoPath);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetDestinationCommand(soldier, At(2, 2))).ShouldFailWith(RuleViolations.NoPath);
    }

    [Fact]
    public void Execute_VisibleUnitOnTarget_ReturnsTileOccupied()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P1, TestRulesets.Settler, 4, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetDestinationCommand(soldier, At(4, 2)));

        result.ShouldFailWith(RuleViolations.TileOccupied);
    }

    [Fact]
    public void Execute_EnemyUnit_ReturnsNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 8, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetDestinationCommand(enemy, At(7, 2)));

        result.ShouldFailWith(RuleViolations.UnitNotFound);
    }

    [Fact]
    public void DestinationPath_FromTheView_GivesThePathAndTurnsTheCommandWouldWalk()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        var preview = GameEngine.DestinationPath(TestRulesets.Default, view, soldier, At(9, 2)).ShouldNotBeNull();

        preview.Path.ShouldBe([At(3, 2), At(4, 2), At(5, 2), At(6, 2), At(7, 2), At(8, 2), At(9, 2)]);
        preview.Turns.ShouldBe(3);
        GameEngine.DestinationPath(TestRulesets.Default, view, soldier, At(3, 2))!.Turns.ShouldBe(0);
    }

    [Fact]
    public void DestinationPath_UnexploredOrEnemysTurn_IsNull()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build();

        GameEngine.DestinationPath(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), soldier, At(9, 6)).ShouldBeNull();
        var p2Turn = state with { CurrentPlayer = P2 };
        GameEngine.DestinationPath(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, p2Turn, P1), soldier, At(3, 2)).ShouldBeNull();
    }

    [Fact]
    public void ViewFor_Destination_ShownOnlyToTheOwner()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 10, 8, destination: (2, 2));
        var state = builder.Build();

        GameEngine.ViewFor(TestRulesets.Default, state, P1).FindOwnUnit(soldier)!.Destination.ShouldBe(At(2, 2));
        GameEngine.ViewFor(TestRulesets.Default, state, P2).Units.Single(unit => unit.Id == soldier).Destination.ShouldBeNull();
    }

    [Fact]
    public void Serialize_UnitWithDestination_RoundTrips()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, destination: (9, 2));

        var restored = GameSerializer.DeserializeState(GameSerializer.Serialize(builder.Build()));

        restored.FindUnit(soldier)!.Destination.ShouldBe(At(9, 2));
    }

    private static GameUpdate Execute(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).ShouldSucceed();
}
