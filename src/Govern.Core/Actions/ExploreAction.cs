using System.Collections.Immutable;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Orders a unit to explore on its own, now and at the start of each of its owner's turns. A
/// <see cref="SetDestinationCommand"/> with no target stops it.
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
public sealed record ExploreCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Sets a unit exploring (see <see cref="AutoMoveRule"/>): it drops its destination and walks at once with the movement it has
/// left, to the nearest tile its owner knows next to one they never saw. A unit with a job can't explore (its owner cancels the
/// job first), and neither can a trader on a route or a unit with no known way to anything unexplored.
/// </summary>
internal static class ExploreRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, ExploreCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (unit.RouteId is not null)
        {
            return RuleViolations.OnTradeRoute;
        }

        if (unit.Job is not null)
        {
            return RuleViolations.UnitBusy;
        }

        if (AutoMoveRule.NearestUnexplored(ruleset, state, unit) is null)
        {
            return RuleViolations.NothingToExplore;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var ordered = state.WithUnit(unit with { Destination = null, Exploring = true });
        return new GameUpdate(AutoMoveRule.Walk(ruleset, ordered, unit.Id, events), events.ToImmutable());
    }
}
