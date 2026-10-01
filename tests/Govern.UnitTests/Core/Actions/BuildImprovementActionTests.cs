using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

// A P1 city at (6, 4) has the territory around it; (5, 5) is next to it.
public class BuildImprovementActionTests
{
    [Fact]
    public void Execute_BuilderOnPlainsInTerritory_StartsAFarmAndUsesItsMovement()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P1, turn: 3);
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm)).ShouldSucceed();

        var working = update.State.FindUnit(unit)!;
        working.MovementLeft.ShouldBe(0);
        working.Job.ShouldBe(UnitJob.ForImprovement(TestRulesets.Farm, 3));
        update.Events.ShouldHaveSingleItem().ShouldBe(new BuildStartedEvent(unit, UnitJob.ForImprovement(TestRulesets.Farm, 3)));
        update.State.Map.TileAt(At(5, 5)).Improvement.ShouldBeNull();
    }

    [Fact]
    public void Execute_AnotherJob_ReplacesTheOldOneAndItsProgress()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Forest, (5, 5));
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: new UnitJob(3, 1, Improvement: TestRulesets.Farm));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.LumberCamp)).ShouldSucceed();

        update.State.FindUnit(unit)!.Job.ShouldBe(UnitJob.ForImprovement(TestRulesets.LumberCamp, 1));
    }

    [Fact]
    public void Execute_SameJobAgain_IsRefusedSoItsProgressIsKept()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: new UnitJob(3, 1, Improvement: TestRulesets.Farm));

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.AlreadyBuilding);
    }

    [Fact]
    public void Execute_OutsideEveryTerritory_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 2);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.NotInTerritory);
    }

    [Fact]
    public void Execute_OnTheCityTile_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 5, 5);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.NotInTerritory);
    }

    [Fact]
    public void Execute_InEnemyTerritory_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P2, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.NotInTerritory);
    }

    [Theory]
    [InlineData("farm", "forest")]
    [InlineData("mine", "plains")]
    [InlineData("lumber_camp", "hills")]
    public void Execute_WrongTerrain_IsRefused(string improvement, string terrain)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(new(terrain), (5, 5));
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, new ImprovementType(improvement)))
            .ShouldFailWith(RuleViolations.WrongTerrain);
    }

    [Fact]
    public void Execute_TileAlreadyImproved_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithImprovement(TestRulesets.Farm, (5, 5));
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.TileImproved);
    }

    [Fact]
    public void Execute_BuildingsTile_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithBuilding(TestRulesets.Campus, (5, 5));
        builder.AddCity(P1, 6, 4, buildings: [TestRulesets.Campus]);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.TileHasBuilding);
    }

    [Fact]
    public void Execute_TileReservedForABuilding_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4, producingBuilding: TestRulesets.Campus, buildingSite: (5, 5));
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.TileHasBuilding);
    }

    [Fact]
    public void Execute_NoMovementLeft_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, movementLeft: 0);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.NoMovementLeft);
    }

    [Theory]
    [InlineData("soldier")]
    [InlineData("settler")]
    public void Execute_UnitWithoutTheAbility_IsRefused(string type)
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, new UnitType(type), 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.CannotBuildImprovements);
    }

    [Theory]
    [InlineData("plantation")]
    [InlineData("")]
    public void Execute_UnknownImprovement_IsRefused(string improvement)
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P1, 6, 4);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, new ImprovementType(improvement)))
            .ShouldFailWith(RuleViolations.UnknownImprovement);
    }

    [Fact]
    public void Execute_EnemyBuilder_ReturnsNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var unit = builder.AddUnit(P2, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new BuildImprovementCommand(unit, TestRulesets.Farm))
            .ShouldFailWith(RuleViolations.UnitNotFound);
    }
}
