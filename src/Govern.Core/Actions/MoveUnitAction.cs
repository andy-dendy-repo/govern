using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Actions;

/// <summary>
/// Moves a unit along the cheapest path to an empty tile within its remaining movement.
/// </summary>
/// <param name="UnitId">The actor's unit.</param>
/// <param name="Target">Destination tile.</param>
public sealed record MoveUnitCommand(UnitId UnitId, HexCoord Target) : GameCommand;

/// <summary>
/// Moves a unit along the cheapest path to an empty tile it can reach with its remaining movement. Moving drops the unit's job.
/// </summary>
internal static class MoveUnitRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, MoveUnitCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (unit.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        // Only a visible blocker is reported as occupied; a hidden one must look like any other unreachable tile.
        if (state.UnitAt(command.Target) is { } blocker && blocker.Id != unit.Id
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
            Job = null,
        };

        return new GameUpdate(state.WithUnit(moved), [new UnitMovedEvent(unit.Id, reachability.PathTo(command.Target))]);
    }
}
