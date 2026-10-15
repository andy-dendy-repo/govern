using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Undo end turn: in a simultaneous game, takes back the actor's ended turn while the others still play, so they may act again.
/// </summary>
public sealed record ResumeTurnCommand : GameCommand;

/// <summary>
/// Clears the actor's <see cref="Player.EndedTurn"/>. Only in a simultaneous game (<c>game.not_simultaneous</c>) and only after
/// they ended this turn (<c>game.turn_not_ended</c>); the round resolves the moment the last player ends, so there is nothing
/// to take back after that.
/// </summary>
internal static class ResumeTurnRule
{
    public static CommandResult Apply(GameState state, PlayerId actor)
    {
        if (state.TurnMode != TurnMode.Simultaneous)
        {
            return RuleViolations.NotSimultaneous;
        }

        var player = state.FindPlayer(actor)!;
        if (!player.EndedTurn)
        {
            return RuleViolations.TurnNotEnded;
        }

        return new GameUpdate(state.WithPlayer(player with { EndedTurn = false }), [new PlayerEndedTurnEvent(actor, Ended: false)]);
    }
}
