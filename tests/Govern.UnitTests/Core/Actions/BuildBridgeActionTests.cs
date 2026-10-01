using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class BuildBridgeActionTests
{
    [Theory]
    [InlineData("builder", "stream")]
    [InlineData("builder", "river")]
    [InlineData("soldier", "deep_river")]
    public void Execute_RiverNextToTheUnit_StartsABridgeAnywhere(string type, string river)
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(new(river), ((5, 5), (6, 5))).WithCurrentPlayer(P1, turn: 2);
        var unit = builder.AddUnit(P1, new UnitType(type), 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5))).ShouldSucceed();

        var working = update.State.FindUnit(unit)!;
        working.MovementLeft.ShouldBe(0);
        working.Job.ShouldBe(UnitJob.ForBridge(At(6, 5), 2));
        update.Events.ShouldHaveSingleItem().ShouldBe(new BuildStartedEvent(unit, UnitJob.ForBridge(At(6, 5), 2)));
        update.State.Map.BridgeBetween(At(5, 5), At(6, 5)).ShouldBeFalse();
    }

    [Fact]
    public void Execute_NoRiver_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.NoRiver);
    }

    [Theory]
    [InlineData(7, 5)]
    [InlineData(5, 5)]
    [InlineData(-1, 5)]
    public void Execute_TargetNotANeighbourOnTheMap_IsRefused(int column, int row)
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((0, 5), (1, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, column == -1 ? 0 : 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(column, row)))
            .ShouldFailWith(RuleViolations.BridgeNotAdjacent);
    }

    [Fact]
    public void Execute_BridgeAlreadyThere_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5))).WithBridge(((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.BridgeExists);
    }

    [Fact]
    public void Execute_FarSideIsALake_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5))).WithTerrain(TestRulesets.Lake, (6, 5));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.BridgeLeadsNowhere);
    }

    [Theory]
    [InlineData(7, 5)]
    [InlineData(4, 5)]
    [InlineData(6, 5)]
    public void Execute_EnemyWorkAreaOnEitherSide_IsRefused(int cityColumn, int cityRow)
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        builder.AddCity(P2, cityColumn, cityRow);
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.EnemyWorkArea);
    }

    [Fact]
    public void Execute_InOwnWorkArea_IsAllowed()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        builder.AddCity(P1, 4, 5);
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5))).ShouldSucceed();
    }

    [Theory]
    [InlineData("settler")]
    [InlineData("archer")]
    public void Execute_UnitWithoutTheAbility_IsRefused(string type)
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, new UnitType(type), 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.CannotBuildBridges);
    }

    [Fact]
    public void Execute_NoMovementLeft_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, movementLeft: 0);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Fact]
    public void Execute_SameBridgeAgain_IsRefusedSoItsProgressIsKept()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((5, 5), (6, 5)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, job: new UnitJob(2, 1, BridgeTo: At(6, 5)));

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildBridgeCommand(unit, At(6, 5)))
            .ShouldFailWith(RuleViolations.AlreadyBuilding);
    }
}
