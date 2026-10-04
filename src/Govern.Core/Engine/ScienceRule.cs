using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;

namespace Govern.Core.Engine;

// The science budget (G11a, the owner's model): science is not stored. A player's science per turn is what their cities make
// of the ruleset's science resource now, and every enabled node takes its points of it. After every command the budget is
// balanced: while the enabled nodes take more than the income, the newest is disabled; then, oldest first, disabled nodes whose
// points fit again (and whose prerequisites are enabled) are enabled, stopping at the first that doesn't fit.
internal static class ScienceRule
{
    // The science commands (unlocking a node, upgrading a unit or a building); null for any other command.
    public static CommandResult? Dispatch(Ruleset ruleset, GameState state, PlayerId actor, GameCommand command) => command switch
    {
        UnlockNodeCommand unlock => UnlockNodeRule.Apply(ruleset, state, actor, unlock),
        UpgradeUnitCommand upgrade => UpgradeUnitRule.Apply(ruleset, state, actor, upgrade),
        UpgradeBuildingCommand upgrade => UpgradeBuildingRule.Apply(ruleset, state, actor, upgrade),
        _ => null,
    };

    // A player's science per turn: the science in the income of each of their cities.
    public static int Income(Ruleset ruleset, GameState state, PlayerId player) => state.Cities
        .Where(city => city.Owner == player)
        .Sum(city => ScienceCalculator.ScienceIn(ruleset, ConstructionRule.Income(ruleset, state.Map, city)));

    public static GameUpdate Balance(Ruleset ruleset, GameUpdate update)
    {
        var state = update.State;
        if (state.Players.All(static player => player.NodeList.IsEmpty))
        {
            return update;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        foreach (var player in state.Players.Where(static player => !player.NodeList.IsEmpty))
        {
            var nodes = Balance(ruleset, player.NodeList, Income(ruleset, state, player.Id), player.Id, events);
            state = state.WithPlayer(player with { Nodes = nodes });
        }

        return events.Count == 0 ? update : new GameUpdate(state, [.. update.Events, .. events]);
    }

    private static ImmutableArray<OwnedNode> Balance(
        Ruleset ruleset,
        ImmutableArray<OwnedNode> nodes,
        int income,
        PlayerId player,
        ImmutableArray<GameEvent>.Builder events)
    {
        var list = nodes.ToList();
        while (ScienceCalculator.Used(ruleset, list) > income)
        {
            var newest = list.FindLastIndex(static node => !node.Disabled);
            list[newest] = list[newest] with { Disabled = true };
            events.Add(new ScienceNodeChangedEvent(player, list[newest].Node, ScienceNodeChange.Disabled));
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (!list[index].Disabled)
            {
                continue;
            }

            var node = ruleset.FindNode(list[index].Node);
            var fits = node is not null
                && node.Points <= income - ScienceCalculator.Used(ruleset, list)
                && node.Requires.All(required => ScienceCalculator.IsEnabled(list, required));
            if (!fits)
            {
                break;
            }

            list[index] = list[index] with { Disabled = false };
            events.Add(new ScienceNodeChangedEvent(player, list[index].Node, ScienceNodeChange.Enabled));
        }

        return [.. list];
    }
}
