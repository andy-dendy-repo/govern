using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// The trade commands' checks (G17) worked out from the viewer's <see cref="PlayerView"/> alone, for the client (the trade
/// counterpart of <see cref="GameEngine"/>'s previews). Each answer equals what <see cref="GameEngine.Execute"/> decides: the view
/// holds every route the viewer is a side of, their own cities' markets, the trade post of every city they may trade with, and
/// (for their own routes) the host's free market.
/// </summary>
public static class TradePreviewCalculator
{
    /// <summary>Whether one of the viewer's traders can be sent to open (or serve) a trade route now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="unitId">The trader.</param>
    /// <param name="homeId">The viewer's city the route starts from.</param>
    /// <param name="hostId">The city at the other end.</param>
    /// <returns>
    /// <see langword="null"/> when an <see cref="OpenTradeRouteCommand"/> would be accepted; otherwise the violation it would be
    /// rejected with.
    /// </returns>
    public static RuleViolation? CheckOpenTradeRoute(Ruleset ruleset, PlayerView view, UnitId unitId, CityId homeId, CityId hostId)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (ViewPreviewCalculator.CheckTurn(view) is { } turn)
        {
            return turn;
        }

        if (ViewPreviewCalculator.ActingUnit(ruleset, view, unitId) is not { } trader)
        {
            return RuleViolations.UnitNotFound;
        }

        if (!ruleset.Unit(trader.Type).CanTrade)
        {
            return RuleViolations.CannotTrade;
        }

        if (trader.RouteId is not null)
        {
            return RuleViolations.OnTradeRoute;
        }

        if (CheckCities(view, homeId, hostId) is { } cities)
        {
            return cities;
        }

        var post = FindCity(view, hostId)!.TradePost!.Value;
        var routes = view.TradeRouteList.Where(route => route.Owner == view.Viewer && route.Home == homeId).ToList();
        if (routes.FirstOrDefault(route => route.Host == hostId) is { } existing)
        {
            return existing.Trader is null ? CheckWay(ruleset, view, trader, post) : RuleViolations.RouteHasTrader;
        }

        return routes.Count >= ruleset.Trade.RoutesPerCity ? RuleViolations.TooManyRoutes : CheckWay(ruleset, view, trader, post);
    }

    /// <summary>Whether a deal can be proposed on one of the viewer's trade routes now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="routeId">The route.</param>
    /// <param name="gives">What the home city would give each turn.</param>
    /// <param name="receives">What the host city would give back each turn.</param>
    /// <returns>
    /// <see langword="null"/> when a <see cref="ProposeDealCommand"/> would be accepted; otherwise the violation it would be
    /// rejected with.
    /// </returns>
    public static RuleViolation? CheckProposeDeal(Ruleset ruleset, PlayerView view, TradeRouteId routeId, IEnumerable<ResourceAmount> gives, IEnumerable<ResourceAmount> receives)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (ViewPreviewCalculator.CheckTurn(view) is { } turn)
        {
            return turn;
        }

        if (view.TradeRouteList.FirstOrDefault(route => route.Id == routeId) is not { } route || route.Owner != view.Viewer)
        {
            return RuleViolations.RouteNotFound;
        }

        if (route.IsActive)
        {
            return RuleViolations.DealActive;
        }

        if (!route.Opened || route.Trader is not { } traderId || view.FindOwnUnit(traderId) is not { } trader
            || route.HostPost is not { } post || !TradeRule.IsAt(trader.Position, post))
        {
            return RuleViolations.TraderNotAtPost;
        }

        var home = FindCity(view, route.Home)!;
        var homeFree = TradeCalculator.FreeMarket(home.MarketList, Deals(view, routeId), home.Id);
        return TradeCalculator.CheckDeal(ruleset, homeFree, route.HostMarket ?? [], gives, receives) is { } problem
            ? ProposeDealRule.Violation(problem)
            : null;
    }

    /// <summary>Whether the viewer can accept a proposal at one of their cities now, after cancelling the given deals.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="routeId">The route carrying the proposal.</param>
    /// <param name="cancelRoutes">Routes whose deals would be cancelled first.</param>
    /// <returns>
    /// <see langword="null"/> when an accepting <see cref="AnswerDealCommand"/> would be accepted; otherwise the violation it would
    /// be rejected with.
    /// </returns>
    public static RuleViolation? CheckAcceptDeal(Ruleset ruleset, PlayerView view, TradeRouteId routeId, IEnumerable<TradeRouteId> cancelRoutes)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(cancelRoutes);
        if (ViewPreviewCalculator.CheckTurn(view) is { } turn)
        {
            return turn;
        }

        if (view.TradeRouteList.FirstOrDefault(route => route.Id == routeId) is not { } route || route.HostOwner != view.Viewer
            || FindCity(view, route.Host) is not { } host || host.Owner != view.Viewer)
        {
            return RuleViolations.RouteNotFound;
        }

        if (route.Deal is not { Accepted: false } deal)
        {
            return RuleViolations.NoProposal;
        }

        var cancelled = cancelRoutes.ToHashSet();
        foreach (var cancelId in cancelled)
        {
            if (view.TradeRouteList.FirstOrDefault(other => other.Id == cancelId) is not { IsActive: true } other || other.Id == route.Id
                || other.Host != route.Host && !(other.Home == route.Host && other.Owner == view.Viewer))
            {
                return RuleViolations.CannotCancelDeal;
            }
        }

        var remaining = view.TradeRouteList
            .Where(other => other.Id != routeId)
            .Select(other => (other.Home, other.Host, cancelled.Contains(other.Id) ? null : other.Deal));
        var homeSide = deal.Gives.Select(static amount => new MarketOrder(amount.Resource, MarketSide.Sell, amount.Amount))
            .Concat(deal.Receives.Select(static amount => new MarketOrder(amount.Resource, MarketSide.Buy, amount.Amount)));
        var hostFree = TradeCalculator.FreeMarket(host.MarketList, remaining, host.Id);
        return TradeCalculator.CheckDeal(ruleset, homeSide, hostFree, deal.Gives, deal.Receives) is { } problem
            ? ProposeDealRule.Violation(problem)
            : null;
    }

    private static RuleViolation? CheckCities(PlayerView view, CityId homeId, CityId hostId)
    {
        if (FindCity(view, homeId) is not { } home || home.Owner != view.Viewer)
        {
            return RuleViolations.CityNotFound;
        }

        if (home.TradePost is null)
        {
            return RuleViolations.NoTradePost;
        }

        return FindCity(view, hostId) is not { TradePost: not null } host || host.Id == home.Id ? RuleViolations.NoTradePartner : null;
    }

    // As AutoMoveRule.Plan for a trader: over the tiles the viewer knows, through a unit they see on the post itself.
    private static RuleViolation? CheckWay(Ruleset ruleset, PlayerView view, Unit trader, HexCoord post)
    {
        if (TradeRule.IsAt(trader.Position, post))
        {
            return null;
        }

        var path = PathFinder.Find(trader.Position, post, ruleset.Unit(trader.Type).Movement, (from, to) => to == post
            ? KnownStepCostOntoPost(ruleset, view, from, to)
            : ViewPreviewCalculator.KnownStepCost(ruleset, view, trader, from, to));
        return path is null ? RuleViolations.NoPath : null;
    }

    // A trade post is never a city's tile, so only its terrain and the river on the way matter.
    private static int? KnownStepCostOntoPost(Ruleset ruleset, PlayerView view, HexCoord from, HexCoord to) =>
        view.TileAt(to).Terrain is { } terrain
            ? ReachabilityCalculator.StepCost(ruleset, UnitDomain.Land, terrain, view.CrossedRiverBetween(from, to))
            : null;

    private static CityView? FindCity(PlayerView view, CityId id) => view.Cities.FirstOrDefault(city => city.Id == id);

    private static IEnumerable<(CityId Home, CityId Host, TradeDeal? Deal)> Deals(PlayerView view, TradeRouteId except) =>
        view.TradeRouteList.Where(route => route.Id != except).Select(static route => (route.Home, route.Host, route.Deal));
}
