using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.Pathfinding;
using Govern.Core.Players;
using Govern.Core.State;
using Govern.Core.Visibility;

namespace Govern.Core.Rules;

/// <summary>
/// Moves a unit along the cheapest path to an empty tile it can reach with its remaining movement.
/// </summary>
internal static class MoveUnitRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, MoveUnitCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (unit.MovementLeft <= 0)
        {
            return GameErrors.NoMovementLeft;
        }

        // Only a visible blocker is reported as occupied; a hidden one must look like any other unreachable tile.
        if (state.UnitAt(command.Target) is { } blocker && blocker.Id != unit.Id
            && VisibilityCalculator.VisibleTiles(state, actor).Contains(command.Target))
        {
            return GameErrors.TileOccupied;
        }

        var reachability = ReachabilityCalculator.Compute(state, unit);
        if (!reachability.CanReach(command.Target))
        {
            return GameErrors.Unreachable;
        }

        var moved = unit with
        {
            Position = command.Target,
            MovementLeft = unit.MovementLeft - reachability.CostTo(command.Target),
        };

        return new GameUpdate(state.WithUnit(moved), [new UnitMovedEvent(unit.Id, reachability.PathTo(command.Target))]);
    }
}
