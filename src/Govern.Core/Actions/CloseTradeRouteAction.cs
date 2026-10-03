using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Closes one of the actor's trade routes (G17): its deal ends and its trader is free again where it stands.
/// </summary>
/// <param name="RouteId">A route the actor opened.</param>
public sealed record CloseTradeRouteCommand(TradeRouteId RouteId) : GameCommand;

/// <summary>Removes a route (see <see cref="CloseTradeRouteCommand"/>); it no longer counts towards its home city's routes.</summary>
internal static class CloseTradeRouteRule
{
    public static CommandResult Apply(GameState state, PlayerId actor, CloseTradeRouteCommand command)
    {
        if (state.FindRoute(command.RouteId) is not { } route || route.Owner != actor)
        {
            return RuleViolations.RouteNotFound;
        }

        var closed = state.WithoutRoute(route.Id);
        if (route.Trader is { } traderId && closed.FindUnit(traderId) is { } trader)
        {
            closed = closed.WithUnit(trader with { RouteId = null, Destination = null });
        }

        return new GameUpdate(closed, [new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Closed, !route.Opened)]);
    }
}
