using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.Actions;

/// <summary>
/// Sends a trader from one of the actor's cities to another city's trade post to open a trade route (G17), or to serve an
/// existing route between the two that has lost its trader.
/// </summary>
/// <param name="TraderId">The actor's trader, not serving a route.</param>
/// <param name="HomeId">The actor's city the route starts from; it needs a trade post.</param>
/// <param name="HostId">The city at the other end, own or enemy, whose trade post the actor sees.</param>
public sealed record OpenTradeRouteCommand(UnitId TraderId, CityId HomeId, CityId HostId) : GameCommand;

/// <summary>
/// Opens (or re-staffs) a route: the trader walks to the host's trade post at once and on each of its owner's turns, without
/// stopping for enemies; the route exists once it gets there. A city opens at most <c>trade.routesPerCity</c> routes.
/// </summary>
internal static class OpenTradeRouteRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, OpenTradeRouteCommand command)
    {
        if (Check(ruleset, state, actor, command) is { } violation)
        {
            return violation;
        }

        var trader = state.FindUnit(command.TraderId)!;
        var host = state.FindCity(command.HostId)!;
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        if (state.TradeRouteList.FirstOrDefault(route => route.Home == command.HomeId && route.Host == host.Id) is { } existing)
        {
            return new GameUpdate(TradeRule.SendTrader(ruleset, state, trader, existing, events), events.ToImmutable());
        }

        var (allocated, id) = state.AllocateId();
        var route = new TradeRoute(new TradeRouteId(id), actor, command.HomeId, host.Id, host.Owner);
        return new GameUpdate(TradeRule.SendTrader(ruleset, allocated.WithRoute(route), trader, route, events), events.ToImmutable());
    }

    private static RuleViolation? Check(Ruleset ruleset, GameState state, PlayerId actor, OpenTradeRouteCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.TraderId, out var trader, out var error))
        {
            return error;
        }

        if (!ruleset.Unit(trader.Type).CanTrade)
        {
            return RuleViolations.CannotTrade;
        }

        if (trader.RouteId is not null)
        {
            return RuleViolations.OnTradeRoute;
        }

        if (CheckCities(ruleset, state, actor, command) is { } violation)
        {
            return violation;
        }

        var post = TradeRule.PostTile(ruleset, state.Map, command.HostId)!.Value;
        if (state.TradeRouteList.FirstOrDefault(route => route.Home == command.HomeId && route.Host == command.HostId) is { } existing)
        {
            return existing.Trader is null ? CheckWay(ruleset, state, trader, post) : RuleViolations.RouteHasTrader;
        }

        return TradeRule.RoutesFrom(state, command.HomeId) >= ruleset.Trade.RoutesPerCity ? RuleViolations.TooManyRoutes : CheckWay(ruleset, state, trader, post);
    }

    // The home city is the actor's and has a trade post; the host is another city whose trade post the actor sees (a hidden
    // city, or a hidden trade post, reads like no partner at all).
    private static RuleViolation? CheckCities(Ruleset ruleset, GameState state, PlayerId actor, OpenTradeRouteCommand command)
    {
        if (state.FindCity(command.HomeId) is not { } home || home.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        if (TradeRule.PostTile(ruleset, state.Map, home.Id) is null)
        {
            return RuleViolations.NoTradePost;
        }

        var visible = VisibilityCalculator.VisibleTiles(ruleset, state, actor);
        return state.FindCity(command.HostId) is not { } host || host.Id == home.Id
            || TradeRule.PostTile(ruleset, state.Map, host.Id) is not { } post
            || host.Owner != actor && (!visible.Contains(host.Position) || !visible.Contains(post))
                ? RuleViolations.NoTradePartner
                : null;
    }

    private static RuleViolation? CheckWay(Ruleset ruleset, GameState state, Unit trader, Map.HexCoord post) =>
        TradeRule.IsAt(trader.Position, post) || AutoMoveRule.Plan(ruleset, state, trader, post, allowOccupiedTarget: true) is not null
            ? null
            : RuleViolations.NoPath;
}
