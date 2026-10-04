using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Stops what a unit is building, so it can move again.
/// </summary>
/// <param name="UnitId">The actor's unit, which has a job.</param>
public sealed record CancelJobCommand(UnitId UnitId) : GameCommand;

/// <summary>
/// Drops a unit's job (see <see cref="UnitJob"/>) with its progress. A unit with a job can't move, so a misclick never stops a
/// build; cancelling is the deliberate way. The movement the job took when it started stays spent.
/// </summary>
internal static class CancelJobRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, CancelJobCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        return unit.Job is null
            ? RuleViolations.NoJob
            : new GameUpdate(state.WithUnit(unit with { Job = null }), []);
    }
}
