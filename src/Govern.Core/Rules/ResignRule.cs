using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Players;
using Govern.Core.State;

namespace Govern.Core.Rules;

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
