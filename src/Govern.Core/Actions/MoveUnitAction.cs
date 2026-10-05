using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Actions;

/// <summary>
/// Moves a unit along the cheapest path to a tile no other unit holds, within its remaining movement (a trader may end on any tile).
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
/// <param name="Target">Destination tile.</param>
public sealed record MoveUnitCommand(UnitId UnitId, HexCoord Target) : GameCommand;

/// <summary>
/// Moves a unit along the cheapest path to an empty tile it can reach with its remaining movement. A unit with a job can't move (its owner cancels the job first); moving drops the unit's destination and its exploring.
/// </summary>
internal static class MoveUnitRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, MoveUnitCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (CheckFree(unit) is { } busy)
        {
            return busy;
        }

        // Only a visible blocker is reported as occupied; a hidden one must look like any other unreachable tile. A trader shares
        // tiles, so nothing occupies a tile for it.
        if (!ruleset.Unit(unit.Type).SharesTiles && state.HolderAt(ruleset, command.Target) is { } blocker && blocker.Id != unit.Id
            && VisibilityCalculator.VisibleTiles(ruleset, state, actor).Contains(command.Target))
        {
            return RuleViolations.TileOccupied;
        }

        var reachability = ReachabilityCalculator.Compute(ruleset, state, unit);
        if (!reachability.CanReach(command.Target))
        {
            return RuleViolations.Unreachable;
        }

        var moved = unit with
        {
            Position = command.Target,
            MovementLeft = unit.MovementLeft - reachability.CostTo(command.Target),
            Destination = null,
            Exploring = false,
        };

        return new GameUpdate(state.WithUnit(moved), [new UnitMovedEvent(unit.Id, unit.Owner, unit.Type, reachability.PathTo(command.Target))]);
    }

    // A trader on an agreed deal, a unit with a job or without movement left can't take a step.
    private static RuleViolation? CheckFree(Unit unit) => unit switch
    {
        { DealId: not null } => RuleViolations.OnTradeDeal,
        { Job: not null } => RuleViolations.UnitBusy,
        { MovementLeft: <= 0 } => RuleViolations.NoMovementLeft,
        _ => null,
    };
}
