using System.Collections.Immutable;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Actions;

/// <summary>
/// Orders a unit to walk to a tile on its own, now and at the start of each of its owner's turns, or cancels that order.
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
/// <param name="Target">
/// The destination: an explored tile with a known path; <see langword="null"/> cancels the order (or the unit's exploring).
/// </param>
public sealed record SetDestinationCommand(UnitId UnitId, HexCoord? Target) : GameCommand;

/// <summary>
/// Gives a unit a destination (see <see cref="AutoMoveRule"/>): it stops exploring and walks at once with the movement it has
/// left. A unit with a job can't be sent (its owner cancels the job first).
/// A target with a unit the actor sees on it is occupied; one with no path over the tiles they know has no path.
/// </summary>
internal static class SetDestinationRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, SetDestinationCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (unit.RouteId is not null)
        {
            return RuleViolations.OnTradeRoute;
        }

        if (command.Target is not { } target)
        {
            return new GameUpdate(state.WithUnit(unit with { Destination = null, Exploring = false }), []);
        }

        if (unit.Job is not null)
        {
            return RuleViolations.UnitBusy;
        }

        // Only a visible blocker is reported as occupied; a hidden one doesn't stop the order (the walk stops in front of it).
        if (state.UnitAt(target) is { } blocker && blocker.Id != unit.Id
            && VisibilityCalculator.VisibleTiles(ruleset, state, actor).Contains(target))
        {
            return RuleViolations.TileOccupied;
        }

        if (AutoMoveRule.Plan(ruleset, state, unit, target) is null)
        {
            return RuleViolations.NoPath;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var ordered = state.WithUnit(unit with { Destination = target, Exploring = false });
        return new GameUpdate(AutoMoveRule.Walk(ruleset, ordered, unit.Id, events), events.ToImmutable());
    }
}
