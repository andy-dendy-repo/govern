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
    public static GameError? CheckTurn(GameState state, PlayerId actor, GameCommand command)
    {
        if (state.Status == GameStatus.Finished)
        {
            return GameErrors.GameFinished;
        }

        if (state.FindPlayer(actor) is not { Status: PlayerStatus.Active })
        {
            return GameErrors.NotAPlayer;
        }

        return command is not ResignCommand && state.CurrentPlayer != actor ? GameErrors.NotYourTurn : null;
    }

    public static bool TryGetOwnedUnit(
        GameState state,
        PlayerId actor,
        UnitId unitId,
        [NotNullWhen(true)] out Unit? unit,
        [NotNullWhen(false)] out GameError? error)
    {
        // Someone else's unit is reported as not found, so probing ids reveals nothing about hidden enemy units.
        unit = state.FindUnit(unitId);
        if (unit is null || unit.Owner != actor)
        {
            unit = null;
            error = GameErrors.UnitNotFound;
            return false;
        }

        error = null;
        return true;
    }
}
