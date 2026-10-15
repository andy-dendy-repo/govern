using Govern.Core.Actions;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Engine;

/// <summary>
/// Runs after every accepted command, once <see cref="VictoryRule"/> has decided who is still playing: in a simultaneous game
/// that goes on, when every active player has ended their turn, the round resolves for all of them
/// (<see cref="EndTurnRule.ResolveRound"/>). So the round ends with the last End turn, and also when the last player still
/// playing the round resigns or is eliminated.
/// </summary>
internal static class RoundRule
{
    public static GameUpdate Apply(Ruleset ruleset, GameUpdate update)
    {
        var state = update.State;
        var waiting = state.Players.Any(static player => player.Status == PlayerStatus.Active && !player.EndedTurn);
        if (state.TurnMode != TurnMode.Simultaneous || state.Status != GameStatus.InProgress || waiting)
        {
            return update;
        }

        var events = update.Events.ToBuilder();
        return new GameUpdate(EndTurnRule.ResolveRound(ruleset, state, events), events.ToImmutable());
    }
}
