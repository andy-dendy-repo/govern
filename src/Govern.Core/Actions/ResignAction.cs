using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Gives up. Allowed at any time, not only on the actor's turn.
/// </summary>
public sealed record ResignCommand : GameCommand;

/// <summary>
/// Marks the actor as resigned; <see cref="VictoryRule"/> then removes their units and decides the game.
/// </summary>
internal static class ResignRule
{
    public static CommandResult Apply(GameState state, PlayerId actor)
    {
        var player = state.FindPlayer(actor)!;
        return new GameUpdate(state.WithPlayer(player with { Status = PlayerStatus.Resigned }), [new PlayerResignedEvent(actor)]);
    }
}
