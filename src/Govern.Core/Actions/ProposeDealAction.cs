using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Proposes a deal on one of the actor's open trade routes (G17): every turn the home city gives <see cref="Gives"/> and the
/// host city gives <see cref="Receives"/> back. A proposal to one's own city is accepted at once; otherwise the host answers it
/// on its turn. A new proposal replaces one still waiting.
/// </summary>
/// <param name="RouteId">The actor's route; its trader stands at the host's trade post.</param>
/// <param name="Gives">What the home city gives each turn: tradable resources it sells and the host buys, within both caps.</param>
/// <param name="Receives">What the host gives back each turn: tradable resources it sells and the home city buys, within both caps.</param>
public sealed record ProposeDealCommand(TradeRouteId RouteId, ImmutableArray<ResourceAmount> Gives, ImmutableArray<ResourceAmount> Receives) : GameCommand;

/// <summary>Puts a proposal on a route (see <see cref="ProposeDealCommand"/>).</summary>
internal static class ProposeDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, ProposeDealCommand command)
    {
        if (state.FindRoute(command.RouteId) is not { } route || route.Owner != actor)
        {
            return RuleViolations.RouteNotFound;
        }

        if (route.IsActive)
        {
            return RuleViolations.DealActive;
        }

        var post = TradeRule.PostTile(ruleset, state.Map, route.Host);
        if (!route.Opened || route.Trader is not { } traderId || state.FindUnit(traderId) is not { } trader
            || post is not { } tile || !TradeRule.IsAt(trader.Position, tile))
        {
            return RuleViolations.TraderNotAtPost;
        }

        var (gives, receives) = (command.Gives.IsDefault ? [] : command.Gives, command.Receives.IsDefault ? [] : command.Receives);
        var home = state.FindCity(route.Home)!;
        var host = state.FindCity(route.Host)!;
        var problem = TradeCalculator.CheckDeal(ruleset, TradeRule.FreeMarket(state, home, route.Id), TradeRule.FreeMarket(state, host, route.Id), gives, receives);
        if (problem is { } found)
        {
            return Violation(found);
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var deal = new TradeDeal(YieldCalculator.Sum(ruleset, gives), YieldCalculator.Sum(ruleset, receives));
        var proposed = route with { Deal = deal };
        events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Proposed));
        var next = state.WithRoute(proposed);
        if (route.HostOwner == actor)
        {
            next = TradeRule.Accept(ruleset, next, proposed, events);
        }

        return new GameUpdate(next, events.ToImmutable());
    }

    // The rule violation for each way a deal's terms can be wrong.
    public static RuleViolation Violation(DealProblem problem) => problem switch
    {
        DealProblem.Empty => RuleViolations.EmptyDeal,
        DealProblem.NotTradable => RuleViolations.NotTradable,
        DealProblem.BadAmount => RuleViolations.BadTradeAmount,
        DealProblem.HomeMarket => RuleViolations.HomeMarketFull,
        _ => RuleViolations.HostMarketFull,
    };
}
