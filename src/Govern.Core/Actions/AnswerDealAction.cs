using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Answers a proposal at one of the actor's cities (G17): accept it, cancelling older deals of that city's market first when
/// its caps have no room (a better offer replaces an older one), or decline it.
/// </summary>
/// <param name="RouteId">A route hosted by one of the actor's cities, carrying a proposal.</param>
/// <param name="Accept">Accept (<see langword="true"/>) or decline.</param>
/// <param name="CancelRoutes">
/// Routes whose accepted deals to cancel before accepting: deals hosted by the same city, or deals the actor opened from it.
/// </param>
public sealed record AnswerDealCommand(TradeRouteId RouteId, bool Accept, ImmutableArray<TradeRouteId>? CancelRoutes = null) : GameCommand;

/// <summary>Accepts or declines a proposal (see <see cref="AnswerDealCommand"/>); the whole answer is rejected if the deal still doesn't fit.</summary>
internal static class AnswerDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, AnswerDealCommand command)
    {
        if (state.FindRoute(command.RouteId) is not { } route || route.HostOwner != actor || state.FindCity(route.Host)?.Owner != actor)
        {
            return RuleViolations.RouteNotFound;
        }

        if (route.Deal is not { Accepted: false } deal)
        {
            return RuleViolations.NoProposal;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        if (!command.Accept)
        {
            events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Declined));
            return new GameUpdate(state.WithRoute(route with { Deal = null }), events.ToImmutable());
        }

        foreach (var cancelId in (command.CancelRoutes ?? []).Distinct())
        {
            if (state.FindRoute(cancelId) is not { IsActive: true } cancelled || cancelled.Id == route.Id
                || cancelled.Host != route.Host && !(cancelled.Home == route.Host && cancelled.Owner == actor))
            {
                return RuleViolations.CannotCancelDeal;
            }

            state = TradeRule.EndDeal(ruleset, state, cancelled, TradeChange.Cancelled, events);
        }

        // Only the host's side is checked here: the home city's caps already hold the proposal (see SetMarketOrderRule).
        var homeSide = deal.Gives.Select(static amount => new MarketOrder(amount.Resource, MarketSide.Sell, amount.Amount))
            .Concat(deal.Receives.Select(static amount => new MarketOrder(amount.Resource, MarketSide.Buy, amount.Amount)));
        var host = state.FindCity(route.Host)!;
        if (TradeCalculator.CheckDeal(ruleset, homeSide, TradeRule.FreeMarket(state, host, route.Id), deal.Gives, deal.Receives) is { } problem)
        {
            return ProposeDealRule.Violation(problem);
        }

        return new GameUpdate(TradeRule.Accept(ruleset, state, state.FindRoute(route.Id)!, events), events.ToImmutable());
    }
}
