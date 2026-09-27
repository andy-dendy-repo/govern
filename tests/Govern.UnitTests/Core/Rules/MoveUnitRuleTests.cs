using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Rules;

public class MoveUnitRuleTests
{
    [Fact]
    public void Execute_ReachableTile_MovesAlongPathAndSpendsMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        var state = builder.Build();

        var update = GameEngine.Execute(state, P1, new MoveUnitCommand(soldier, At(4, 2))).ShouldSucceed();

        var moved = update.State.FindUnit(soldier)!;
        moved.Position.ShouldBe(At(4, 2));
        moved.MovementLeft.ShouldBe(0);
        update.State.Version.ShouldBe(state.Version + 1);
        var movedEvent = update.Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>();
        movedEvent.Path.ShouldBe([At(3, 2), At(4, 2)]);
    }

    [Fact]
    public void Execute_OtherPlayersTurn_ReturnsNotYourTurn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P2, UnitType.Soldier, 8, 2);

        var result = GameEngine.Execute(builder.Build(), P2, new MoveUnitCommand(soldier, At(7, 2)));

        result.ShouldFailWith(GameErrors.NotYourTurn);
    }

    [Fact]
    public void Execute_EnemyUnit_ReturnsNotFoundLikeAMissingId()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, UnitType.Soldier, 8, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(enemy, At(7, 2)));

        result.ShouldFailWith(GameErrors.UnitNotFound);
    }

    [Fact]
    public void Execute_HiddenEnemyOnTarget_ReturnsUnreachableLikeAnyFarTile()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddUnit(P2, UnitType.Soldier, 8, 6);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(8, 6)));

        result.ShouldFailWith(GameErrors.Unreachable);
    }

    [Fact]
    public void Execute_OwnTile_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(2, 2)));

        result.ShouldFailWith(GameErrors.Unreachable);
    }

    [Fact]
    public void Execute_TwoSingleSteps_SpendMovementOneAtATime()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);

        var first = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed().State;
        var second = GameEngine.Execute(first, P1, new MoveUnitCommand(soldier, At(4, 2))).ShouldSucceed().State;

        first.FindUnit(soldier)!.MovementLeft.ShouldBe(1);
        second.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
        GameEngine.Execute(second, P1, new MoveUnitCommand(soldier, At(5, 2))).ShouldFailWith(GameErrors.NoMovementLeft);
    }

    [Fact]
    public void Execute_UnknownUnit_ReturnsNotFound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var result = GameEngine.Execute(state, P1, new MoveUnitCommand(new UnitId(999), At(3, 3)));

        result.ShouldFailWith(GameErrors.UnitNotFound);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(-1, 2)]
    [InlineData(2, Height)]
    public void Execute_TooFarOrOffMap_ReturnsUnreachable(int column, int row)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(column, row)));

        result.ShouldFailWith(GameErrors.Unreachable);
    }

    [Fact]
    public void Execute_OccupiedTarget_ReturnsOccupied()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddUnit(P1, UnitType.Settler, 3, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(GameErrors.TileOccupied);
    }

    [Fact]
    public void Execute_ShortestPathBlockedByUnit_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddUnit(P2, UnitType.Soldier, 3, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(4, 2)));

        result.ShouldFailWith(GameErrors.Unreachable);
    }

    [Fact]
    public void Execute_EnemyCityTile_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddCity(P2, 3, 2);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(GameErrors.Unreachable);
    }

    [Fact]
    public void Execute_OwnCityTile_EntersTheCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddCity(P1, 3, 2);

        var update = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.Position.ShouldBe(At(3, 2));
    }

    [Fact]
    public void Execute_NoMovementLeft_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2, movementLeft: 0);

        var result = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(GameErrors.NoMovementLeft);
    }

    [Fact]
    public void Execute_FinishedGame_ReturnsGameFinished()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        var state = builder.Build() with { Status = GameStatus.Finished, Winner = P1 };

        var result = GameEngine.Execute(state, P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(GameErrors.GameFinished);
    }
}
