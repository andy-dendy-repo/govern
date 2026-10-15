using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class ResumeTurnActionTests
{
    [Fact]
    public void Execute_AfterEndingASimultaneousTurn_ThePlayerMayActAgain()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var ended = GameEngine.Execute(TestRulesets.Default, Simultaneous(builder), P1, new EndTurnCommand()).ShouldSucceed().State;

        var update = GameEngine.Execute(TestRulesets.Default, ended, P1, new ResumeTurnCommand()).ShouldSucceed();

        update.State.FindPlayer(P1)!.EndedTurn.ShouldBeFalse();
        update.Events.ShouldBe([new PlayerEndedTurnEvent(P1, Ended: false)]);
        GameEngine.Execute(TestRulesets.Default, update.State, P1, new MoveUnitCommand(soldier, At(3, 2))).ShouldSucceed();
    }

    [Fact]
    public void Execute_TurnNotEnded_IsRejected()
    {
        var state = Simultaneous(new GameStateBuilder().WithCapitals());

        GameEngine.Execute(TestRulesets.Default, state, P1, new ResumeTurnCommand()).ShouldFailWith(RuleViolations.TurnNotEnded);
    }

    [Fact]
    public void Execute_SequentialGame_IsRejected()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ResumeTurnCommand()).ShouldFailWith(RuleViolations.NotSimultaneous);
        GameEngine.Execute(TestRulesets.Default, state, P2, new ResumeTurnCommand()).ShouldFailWith(RuleViolations.NotYourTurn);
    }

    private static GameState Simultaneous(GameStateBuilder builder) => builder.Build() with { TurnMode = TurnMode.Simultaneous };
}
