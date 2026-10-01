using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class MoveUnitActionTests
{
    [Fact]
    public void Execute_ReachableTile_MovesAlongPathAndSpendsMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(soldier, At(4, 2))).ShouldSucceed();

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
        var soldier = builder.AddUnit(P2, TestRulesets.Soldier, 8, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P2, new MoveUnitCommand(soldier, At(7, 2)));

        result.ShouldFailWith(RuleViolations.NotYourTurn);
    }

    [Fact]
    public void Execute_EnemyUnit_ReturnsNotFoundLikeAMissingId()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 8, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(enemy, At(7, 2)));

        result.ShouldFailWith(RuleViolations.UnitNotFound);
    }

    [Fact]
    public void Execute_HiddenEnemyOnTarget_ReturnsUnreachableLikeAnyFarTile()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 8, 6);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(8, 6)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_OwnTile_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(2, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_TwoSingleSteps_SpendMovementOneAtATime()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var first = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed().State;
        var second = GameEngine.Execute(TestRulesets.Default, first, P1, new MoveUnitCommand(soldier, At(4, 2))).ShouldSucceed().State;

        first.FindUnit(soldier)!.MovementLeft.ShouldBe(1);
        second.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
        GameEngine.Execute(TestRulesets.Default, second, P1, new MoveUnitCommand(soldier, At(5, 2))).ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_UnknownUnit_ReturnsNotFound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(new UnitId(999), At(3, 3)));

        result.ShouldFailWith(RuleViolations.UnitNotFound);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(-1, 2)]
    [InlineData(2, Height)]
    public void Execute_TooFarOrOffMap_ReturnsUnreachable(int column, int row)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(column, row)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_OccupiedTarget_ReturnsOccupied()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P1, TestRulesets.Settler, 3, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.TileOccupied);
    }

    [Fact]
    public void Execute_ShortestPathBlockedByUnit_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 3, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(4, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_EnemyCityTile_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddCity(P2, 3, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_OwnCityTile_EntersTheCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddCity(P1, 3, 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.Position.ShouldBe(At(3, 2));
    }

    [Fact]
    public void Execute_NoMovementLeft_ReturnsNoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 0);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_FinishedGame_ReturnsGameFinished()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build() with { Status = GameStatus.Finished, Winner = P1 };

        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.GameFinished);
    }

    [Fact]
    public void Execute_RulesetMovementAndTerrainCost_BoundTheMove()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["units"]![1]!["movement"] = 4;
            json["units"]![1]!["sight"] = 2;
            json["terrains"]![0]!["movementCost"] = 2;
        });
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 4);
        var state = builder.Build();

        GameEngine.Execute(ruleset, state, P1, new MoveUnitCommand(soldier, At(4, 2))).ShouldSucceed().State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
        GameEngine.Execute(ruleset, state, P1, new MoveUnitCommand(soldier, At(5, 2))).ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_IntoForest_SpendsTwoMovement()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Forest, (3, 2));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
    }

    [Fact]
    public void Execute_ForestWithOneMovementLeft_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Forest, (3, 2));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 1);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Theory]
    [InlineData("mountains")]
    [InlineData("lake")]
    public void Execute_ImpassableTerrain_ReturnsUnreachable(string terrain)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(new(terrain), (3, 2));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_OverAStream_CostsOnlyTheTile()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.Stream, ((2, 2), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 1);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
    }

    [Fact]
    public void Execute_OverARiver_CostsOneMore()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((2, 2), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
    }

    [Fact]
    public void Execute_OverARiverWithOneMovementLeft_ReturnsUnreachable()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((2, 2), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 1);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_TheSameEdgeFromTheOtherSide_CostsTheSame()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((2, 2), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 3, 2, movementLeft: 1);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(2, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_DeepRiverOnEveryNearEdge_ReturnsUnreachable()
    {
        // The deep river walls off (3, 2) from (2, 2) and both tiles next to them; the way round is 3 steps, one too many.
        var builder = new GameStateBuilder().WithCapitals()
            .WithRiver(TestRulesets.DeepRiver, ((2, 2), (3, 2)), ((2, 1), (3, 2)), ((2, 3), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));

        result.ShouldFailWith(RuleViolations.Unreachable);
    }

    [Fact]
    public void Execute_DeepRiverOnTheDirectEdge_GoesAroundIt()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.DeepRiver, ((2, 2), (3, 2)));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();

        update.State.FindUnit(soldier)!.MovementLeft.ShouldBe(0);
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>().Path.Length.ShouldBe(2);
    }
}
