using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Science;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class UnlockNodeActionTests
{
    [Fact]
    public void Execute_FreeScienceCoversTheNode_UnlocksItAtOnce()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new UnlockNodeCommand("archery")).ShouldSucceed();

        update.State.FindPlayer(P1)!.NodeList.ShouldBe([new OwnedNode("archery")]);
        update.Events.ShouldBe([new ScienceNodeChangedEvent(P1, "archery", ScienceNodeChange.Unlocked)]);
        GameEngine.Execute(TestRulesets.Default, update.State, P1, new SetProductionCommand(state.Cities[0].Id, TestRulesets.Archer)).ShouldSucceed();
    }

    [Fact]
    public void Execute_NodeTakesMoreThanTheFreeScience_IsRefused()
    {
        // One city makes 1 science a turn; Archery already takes it.
        var state = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery").Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new UnlockNodeCommand("sailing")).ShouldFailWith(RuleViolations.NotEnoughScience);
    }

    [Fact]
    public void Execute_ScienceIsAPerTurnBudget_NotAStock()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var state = builder.Build();

        // Ending turns doesn't let science pile up: the city still makes 1 a turn, and Education takes 2.
        var later = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed().State;
        later = GameEngine.Execute(TestRulesets.Default, later, P2, new EndTurnCommand()).ShouldSucceed().State;

        GameEngine.ViewFor(TestRulesets.Default, later, P1).Science!.Income.ShouldBe(1);
        GameEngine.Execute(TestRulesets.Default, later, P1, new UnlockNodeCommand("education")).ShouldFailWith(RuleViolations.NotEnoughScience);
    }

    [Theory]
    [InlineData("alchemy", "science.unknown_node")]
    [InlineData("archery", "science.node_owned")]
    [InlineData("stirrups", "science.needs_prerequisite")]
    [InlineData("iron_working", "science.needs_prerequisite")]
    public void Execute_InvalidNode_IsRefused(string node, string code)
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery");
        builder.AddCity(P1, 5, 5);
        builder.AddCity(P1, 8, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new UnlockNodeCommand(node)).Violation!.Code.ShouldBe(code);
    }

    [Fact]
    public void Execute_PrerequisiteDisabled_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, new OwnedNode("archery", Disabled: true));
        builder.AddCity(P1, 5, 5);
        builder.AddCity(P1, 8, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new UnlockNodeCommand("machinery")).ShouldFailWith(RuleViolations.NodeNeedsPrerequisite);
    }

    [Fact]
    public void CheckUnlockNode_FromTheView_MatchesExecute()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery");
        builder.AddCity(P1, 5, 5);
        var state = builder.Build();
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);
        var otherView = GameEngine.ViewFor(TestRulesets.Default, state, P2);

        foreach (var node in TestRulesets.Default.Science.Nodes.Select(static node => node.Id).Append("alchemy"))
        {
            SciencePreviewCalculator.CheckUnlockNode(TestRulesets.Default, view, node)
                .ShouldBe(GameEngine.Execute(TestRulesets.Default, state, P1, new UnlockNodeCommand(node)).Violation, node);
            SciencePreviewCalculator.CheckUnlockNode(TestRulesets.Default, otherView, node).ShouldBe(RuleViolations.NotYourTurn);
        }
    }
}
