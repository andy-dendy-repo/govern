using Govern.Core.Actions;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Engine;

// Sends a command to the rule that applies it. A new action adds its case here; the trade and science commands are routed by
// TradeRule.Dispatch and ScienceRule.Dispatch. An unknown command type is a programming error.
internal static class CommandDispatcher
{
    public static CommandResult Dispatch(Ruleset ruleset, GameState state, PlayerId actor, GameCommand command) => command switch
    {
        MoveUnitCommand move => MoveUnitRule.Apply(ruleset, state, actor, move),
        SetDestinationCommand destination => SetDestinationRule.Apply(ruleset, state, actor, destination),
        ExploreCommand explore => ExploreRule.Apply(ruleset, state, actor, explore),
        CancelJobCommand cancel => CancelJobRule.Apply(state, actor, cancel),
        AttackCommand attack => AttackRule.Apply(ruleset, state, actor, attack),
        FoundCityCommand found => FoundCityRule.Apply(ruleset, state, actor, found),
        SetProductionCommand production => SetProductionRule.Apply(ruleset, state, actor, production),
        BuildImprovementCommand build => BuildImprovementRule.Apply(ruleset, state, actor, build),
        BuildBridgeCommand bridge => BuildBridgeRule.Apply(ruleset, state, actor, bridge),
        ProduceBuildingCommand building => ProduceBuildingRule.Apply(ruleset, state, actor, building),
        SetUpkeepPriorityCommand priority => SetUpkeepPriorityRule.Apply(state, actor, priority),
        EndTurnCommand => EndTurnRule.Apply(ruleset, state, actor),
        ResignCommand => ResignRule.Apply(state, actor),
        _ => TradeRule.Dispatch(ruleset, state, actor, command)
            ?? ScienceRule.Dispatch(ruleset, state, actor, command)
            ?? throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unknown command."),
    };
}
