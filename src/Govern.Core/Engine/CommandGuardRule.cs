using System.Diagnostics.CodeAnalysis;
using Govern.Core.Actions;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Engine;

/// <summary>
/// Checks shared by every command: the game runs, the actor plays, it is their turn, and they own the unit they use.
/// Rejections never reveal hidden information (see <c>docs/features/game-core.md</c>, fog of war).
/// </summary>
internal static class CommandGuardRule
{
    public static RuleViolation? CheckTurn(GameState state, PlayerId actor, GameCommand command) =>
        CheckTurn(state.Status, state.FindPlayer(actor)?.Status, command is ResignCommand || state.CurrentPlayer == actor);

    // Shared with the view-based previews (ViewPreviewCalculator), so both check the turn the same way.
    public static RuleViolation? CheckTurn(GameStatus status, PlayerStatus? actorStatus, bool mayActNow)
    {
        if (status == GameStatus.Finished)
        {
            return RuleViolations.GameFinished;
        }

        if (actorStatus is not PlayerStatus.Active)
        {
            return RuleViolations.NotAPlayer;
        }

        return mayActNow ? null : RuleViolations.NotYourTurn;
    }

    public static bool TryGetOwnedUnit(
        GameState state,
        PlayerId actor,
        UnitId unitId,
        [NotNullWhen(true)] out Unit? unit,
        [NotNullWhen(false)] out RuleViolation? error)
    {
        // Someone else's unit is reported as not found, so probing ids reveals nothing about hidden enemy units.
        unit = state.FindUnit(unitId);
        if (unit is null || unit.Owner != actor)
        {
            unit = null;
            error = RuleViolations.UnitNotFound;
            return false;
        }

        error = null;
        return true;
    }
}
