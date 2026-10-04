using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Unlocks a science node at once (G11a). The node takes its points of the actor's science every turn from now on; it can't be
/// given up.
/// </summary>
/// <param name="Node">The node: a node id of the game's ruleset.</param>
public sealed record UnlockNodeCommand(string Node) : GameCommand;

/// <summary>
/// Adds a node to the actor's nodes when it exists, isn't owned yet, its prerequisites are owned and enabled, and the actor's
/// free science per turn (income less the points of their enabled nodes) covers its points.
/// </summary>
internal static class UnlockNodeRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, UnlockNodeCommand command)
    {
        var player = state.FindPlayer(actor)!;
        if (ScienceCalculator.CheckUnlock(ruleset, player.NodeList, ScienceRule.Income(ruleset, state, actor), command.Node) is { } violation)
        {
            return violation;
        }

        var unlocked = player with { Nodes = [.. player.NodeList, new OwnedNode(command.Node)] };
        return new GameUpdate(state.WithPlayer(unlocked), [new ScienceNodeChangedEvent(actor, command.Node, ScienceNodeChange.Unlocked)]);
    }
}
