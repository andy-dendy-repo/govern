using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Cancels a trade route's deal or proposal (G17), from either side: the route stays, and its trader goes back to the host's
/// trade post to wait for a new proposal.
/// </summary>
/// <param name="RouteId">A route the actor opened, or one hosted by one of the actor's cities.</param>
public sealed record CancelDealCommand(TradeRouteId RouteId) : GameCommand;

/// <summary>Ends a route's deal (see <see cref="CancelDealCommand"/>).</summary>
internal static class CancelDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, CancelDealCommand command)
    {
        if (state.FindRoute(command.RouteId) is not { } route || (route.Owner != actor && route.HostOwner != actor))
        {
            return RuleViolations.RouteNotFound;
        }

        if (route.Deal is null)
        {
            return RuleViolations.NoDeal;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        return new GameUpdate(TradeRule.EndDeal(ruleset, state, route, TradeChange.Cancelled, events), events.ToImmutable());
    }
}
