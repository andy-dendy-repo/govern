using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class CancelJobActionTests
{
    [Fact]
    public void Execute_UnitWithAJob_DropsItSoTheUnitCanMove()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var worker = builder.AddUnit(P1, TestRulesets.Builder, 5, 5, job: new UnitJob(3, 1, Improvement: TestRulesets.Farm));
        var state = builder.Build();
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new CancelJobCommand(worker)).ShouldSucceed();

        GameEngine.ReachableTiles(TestRulesets.Default, view, worker).ShouldBeEmpty();
        update.State.FindUnit(worker)!.Job.ShouldBeNull();
        update.Events.ShouldBeEmpty();
        GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, update.State, P1), worker).ShouldNotBeEmpty();
        GameEngine.Execute(TestRulesets.Default, update.State, P1, new MoveUnitCommand(worker, At(4, 5))).ShouldSucceed();
    }

    [Fact]
    public void Execute_NoJob_FailsWithNoJob()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var worker = builder.AddUnit(P1, TestRulesets.Builder, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new CancelJobCommand(worker)).ShouldFailWith(RuleViolations.NoJob);
    }

    [Fact]
    public void Execute_EnemyUnit_FailsWithUnitNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, TestRulesets.Builder, 9, 7, job: new UnitJob(1, 1, Improvement: TestRulesets.Farm));

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new CancelJobCommand(enemy)).ShouldFailWith(RuleViolations.UnitNotFound);
    }
}
