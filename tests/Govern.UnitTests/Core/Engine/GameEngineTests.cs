using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Players;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

public class GameEngineTests
{
    [Fact]
    public void Execute_AcceptedCommand_IncrementsVersionAndLeavesInputUntouched()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.Version.ShouldBe(1);
        state.Version.ShouldBe(0);
        state.CurrentPlayer.ShouldBe(P1);
    }

    [Fact]
    public void Execute_ActorOutsideTheGame_ReturnsNotAPlayer()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var result = GameEngine.Execute(state, new PlayerId(Guid.NewGuid()), new EndTurnCommand());

        result.ShouldFailWith(GameErrors.NotAPlayer);
    }

    [Fact]
    public void Execute_UnknownCommandType_Throws()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        Should.Throw<ArgumentOutOfRangeException>(() => GameEngine.Execute(state, P1, new UnknownCommand()));
    }

    [Fact]
    public void ReachableTiles_SoldierInOpenField_ReturnsEveryTileWithinItsMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 5, 5);

        var tiles = GameEngine.ReachableTiles(builder.Build(), P1, soldier);

        tiles.Length.ShouldBe(18);
        tiles.Select(tile => tile.DistanceTo(At(5, 5))).ShouldAllBe(distance => distance == 1 || distance == 2);
    }

    [Fact]
    public void ReachableTiles_MapCorner_ExcludesTilesOffTheMap()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P2, 11, 9);
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 0, 0);

        var tiles = GameEngine.ReachableTiles(builder.Build(), P1, soldier);

        tiles.ShouldNotBeEmpty();
        tiles.ShouldAllBe(tile => tile.Column >= 0 && tile.Row >= 0);
    }

    [Fact]
    public void ReachableTiles_UnitWithoutMovementOrUnknown_ReturnsEmpty()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var tired = builder.AddUnit(P1, UnitType.Soldier, 5, 5, movementLeft: 0);
        var state = builder.Build();

        GameEngine.ReachableTiles(state, P1, tired).ShouldBeEmpty();
        GameEngine.ReachableTiles(state, P1, new UnitId(999)).ShouldBeEmpty();
    }

    [Fact]
    public void ReachableTiles_EnemyUnit_ReturnsEmptySoHiddenUnitsStayHidden()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, UnitType.Soldier, 8, 6);

        GameEngine.ReachableTiles(builder.Build(), P1, enemy).ShouldBeEmpty();
    }

    [Fact]
    public void Execute_SameCommandsReplayed_ProducesIdenticalState()
    {
        GameSerializer.Serialize(Play(seed: 7)).ShouldBe(GameSerializer.Serialize(Play(seed: 7)));
    }

    // Each player moves their first movable unit to its first reachable tile, then ends the turn, for four rounds.
    private static GameState Play(ulong seed)
    {
        var state = GameFactory.Create(new GameSetup([P1, P2], seed));
        for (var i = 0; i < 8; i++)
        {
            var unit = state.Units.First(unit => unit.Owner == state.CurrentPlayer && unit.MovementLeft > 0);
            var target = GameEngine.ReachableTiles(state, state.CurrentPlayer, unit.Id)[0];
            state = GameEngine.Execute(state, state.CurrentPlayer, new MoveUnitCommand(unit.Id, target)).ShouldSucceed().State;
            state = GameEngine.Execute(state, state.CurrentPlayer, new EndTurnCommand()).ShouldSucceed().State;
        }

        state.Version.ShouldBe(16);
        state.Turn.ShouldBe(5);
        return state;
    }

    private sealed record UnknownCommand : GameCommand;
}
