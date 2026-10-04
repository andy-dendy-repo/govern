using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Engine;

// Trading (G17, the owner's model): traders walk between two cities' trade posts, accepted deals swap goods at the end of the
// host owner's turn, and each city's demand levels move with what it wants and offers. The actions (Actions/*Trade*, *Deal*,
// SetMarketOrder) open routes and settle deals; this rule runs the parts that happen on their own.
internal static class TradeRule
{
    // The trade commands' share of CommandDispatcher.Dispatch; null for any other command.
    public static CommandResult? Dispatch(Ruleset ruleset, GameState state, PlayerId actor, GameCommand command) => command switch
    {
        SetMarketOrderCommand market => SetMarketOrderRule.Apply(ruleset, state, actor, market),
        OpenTradeRouteCommand open => OpenTradeRouteRule.Apply(ruleset, state, actor, open),
        ProposeDealCommand propose => ProposeDealRule.Apply(ruleset, state, actor, propose),
        AnswerDealCommand answer => AnswerDealRule.Apply(ruleset, state, actor, answer),
        CancelDealCommand cancel => CancelDealRule.Apply(ruleset, state, actor, cancel),
        CloseTradeRouteCommand close => CloseTradeRouteRule.Apply(state, actor, close),
        _ => null,
    };

    // The tile of a city's trade post (the ruleset's trade building standing on its territory); null when it has none. A game
    // saved before buildings had sites has a trade post with no tile, which can't trade.
    public static HexCoord? PostTile(Ruleset ruleset, GameMap map, CityId city) =>
        map.Tiles.FirstOrDefault(tile => tile.Territory == city && tile.Building is { } building && ruleset.IsKindOf(building, ruleset.Trade.Building))?.Coord;

    // A trader is at a trade post when it stands next to it (or on it: a trader never walks onto it, but may start there).
    public static bool IsAt(HexCoord position, HexCoord post) => position.DistanceTo(post) <= 1;

    // Every route's deal, for TradeCalculator, except one being decided.
    public static IEnumerable<(CityId Home, CityId Host, TradeDeal? Deal)> Deals(GameState state, TradeRouteId? except = null) =>
        state.TradeRouteList.Where(route => route.Id != except).Select(static route => (route.Home, route.Host, route.Deal));

    // A city's market with what its deals already use taken off each cap.
    public static ImmutableArray<MarketOrder> FreeMarket(GameState state, City city, TradeRouteId? except = null) =>
        TradeCalculator.FreeMarket(city.MarketList, Deals(state, except), city.Id);

    // How many routes a city has opened (with or without a trader, opened or on the way).
    public static int RoutesFrom(GameState state, CityId city) => state.TradeRouteList.Count(route => route.Home == city);

    // Sends a route's trader to the host's trade post: it drops any job and walks at once with the movement it has left.
    public static GameState SendTrader(Ruleset ruleset, GameState state, Unit trader, TradeRoute route, ImmutableArray<GameEvent>.Builder events)
    {
        var post = PostTile(ruleset, state.Map, route.Host);
        var sent = state.WithRoute(route with { Trader = trader.Id }).WithUnit(trader with { RouteId = route.Id, Destination = post, Job = null });
        return Walk(ruleset, sent, sent.FindUnit(trader.Id)!, events);
    }

    // A trader's walk (AutoMoveRule.Walk hands it over): towards its destination, a trade post, without stopping for enemies
    // it sights. Next to the post it has arrived: with an accepted deal it turns round for the other post, otherwise it waits
    // there (and the route opens on its first arrival). With no known way, the route pauses until there is one again.
    public static GameState Walk(Ruleset ruleset, GameState state, Unit unit, ImmutableArray<GameEvent>.Builder events)
    {
        if (unit.RouteId is not { } routeId || state.FindRoute(routeId) is not { } route || route.Trader != unit.Id)
        {
            return state.WithUnit(unit with { RouteId = null, Destination = null });
        }

        if (unit.Destination is not { } target)
        {
            return state;
        }

        if (IsAt(unit.Position, target))
        {
            return Arrive(ruleset, state, unit, route, events);
        }

        if (AutoMoveRule.Plan(ruleset, state, unit, target, allowOccupiedTarget: true) is not { } path)
        {
            return route.Paused ? state : state.WithRoute(route with { Paused = true });
        }

        // It stops on the first tile next to the post, leaving the post itself free.
        var (walkedState, walker, walked, _) = AutoMoveRule.TakeSteps(ruleset, state, unit, ToPost(path, target), stopOnSighting: false);
        if (walked.Length > 0)
        {
            events.Add(new UnitMovedEvent(walker.Id, walker.Owner, walker.Type, walked));
        }

        var moving = route.Paused ? walkedState.WithRoute(route with { Paused = false }) : walkedState;
        return IsAt(walker.Position, target) ? Arrive(ruleset, moving, walker, moving.FindRoute(routeId)!, events) : moving;
    }

    // Swaps the goods of every accepted deal hosted by one of the actor's cities, oldest deal first: the home city gives its part
    // and the host its part, or, when either lacks its part, nothing moves this turn. A paused route swaps nothing.
    public static GameState Exchange(Ruleset ruleset, GameState state, PlayerId actor, ImmutableArray<GameEvent>.Builder events)
    {
        var routes = state.TradeRouteList
            .Where(route => route is { IsActive: true, Paused: false, Trader: not null } && route.HostOwner == actor)
            .OrderBy(static route => route.Deal!.AcceptedTurn)
            .ThenBy(static route => route.Id.Value)
            .ToList();

        foreach (var route in routes)
        {
            if (state.FindCity(route.Home) is not { } home || state.FindCity(route.Host) is not { } host)
            {
                continue;
            }

            var deal = route.Deal!;
            var homeShort = !UpkeepCalculator.Missing(ruleset, home.StockList, deal.Gives).IsEmpty;
            var hostShort = !UpkeepCalculator.Missing(ruleset, host.StockList, deal.Receives).IsEmpty;
            events.Add(new TradeExchangedEvent(route.Id, route.Owner, route.HostOwner, home.Id, host.Id, deal.Gives, deal.Receives, homeShort, hostShort));
            if (homeShort || hostShort)
            {
                continue;
            }

            // A deal with one's own city (home == host is impossible) moves goods between two stocks of one player.
            state = state.WithCity(WithStock(home, Swap(ruleset, home.StockList, deal.Gives, deal.Receives)));
            host = state.FindCity(route.Host)!;
            state = state.WithCity(WithStock(host, Swap(ruleset, host.StockList, deal.Receives, deal.Gives)));
        }

        return state;
    }

    // Moves the demand levels of the actor's trading cities (those with a trade post) after their deals swapped: per tradable
    // resource, need is the free cap of a buy order plus the upkeep in it the city couldn't pay this turn, and proposition the
    // free cap of a sell order.
    public static GameState UpdateDemand(Ruleset ruleset, GameState state, PlayerId actor, IReadOnlyDictionary<CityId, ImmutableArray<ResourceAmount>> shortages)
    {
        foreach (var city in state.Cities.Where(city => city.Owner == actor).OrderBy(static city => city.Id.Value).ToList())
        {
            if (PostTile(ruleset, state.Map, city.Id) is null)
            {
                continue;
            }

            var free = FreeMarket(state, city);
            var unpaid = shortages.GetValueOrDefault(city.Id, []);
            var levels = ImmutableArray.CreateBuilder<ResourceDemand>();
            foreach (var resource in ruleset.TradableResources)
            {
                var order = free.FirstOrDefault(order => order.Resource == resource.Id);
                var need = (order is { Side: MarketSide.Buy } ? order.Cap : 0) + YieldCalculator.AmountOf(unpaid, resource.Id);
                var proposition = order is { Side: MarketSide.Sell } ? order.Cap : 0;
                var percent = TradeCalculator.NextDemand(ruleset.Trade, TradeCalculator.DemandPercent(city.DemandList, resource.Id), need, proposition);
                if (percent != TradeRules.BaseDemandPercent)
                {
                    levels.Add(new ResourceDemand(resource.Id, percent));
                }
            }

            var demand = levels.ToImmutable();
            state = state.WithCity(city with { Demand = demand.IsEmpty ? null : demand });
        }

        return state;
    }

    // After every command: a route whose home or host city changed hands ends (its trader is freed where it stands), a route
    // whose trader died loses its deal (the route stays for a new trader), and a unit left pointing at a route it doesn't serve
    // is freed.
    public static GameUpdate Reconcile(GameUpdate update)
    {
        var state = update.State;
        if (state.TradeRouteList.IsEmpty && !state.Units.Any(static unit => unit.RouteId is not null))
        {
            return update;
        }

        var events = update.Events.ToBuilder();
        foreach (var route in state.TradeRouteList)
        {
            var home = state.FindCity(route.Home);
            var host = state.FindCity(route.Host);
            if (home?.Owner != route.Owner || host?.Owner != route.HostOwner)
            {
                state = state.WithoutRoute(route.Id);
                events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Closed, !route.Opened));
            }
            else if (route.Trader is { } trader && state.FindUnit(trader) is null)
            {
                state = state.WithRoute(route with { Trader = null, Deal = null, Paused = false });
                if (route.Deal is not null)
                {
                    events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Broken));
                }
            }
        }

        var stray = state.Units.Where(unit => unit.RouteId is { } id && state.FindRoute(id)?.Trader != unit.Id).ToList();
        state = stray.Aggregate(state, static (current, unit) => current.WithUnit(unit with { RouteId = null, Destination = null }));
        return new GameUpdate(state, events.ToImmutable());
    }

    // Ends a route's deal: the trader (if alive) goes back to the host's trade post to wait for a new proposal.
    public static GameState EndDeal(Ruleset ruleset, GameState state, TradeRoute route, TradeChange change, ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, change));
        var ended = state.WithRoute(route with { Deal = null, Paused = false });
        if (route.Trader is not { } traderId || ended.FindUnit(traderId) is not { } trader)
        {
            return ended;
        }

        var post = PostTile(ruleset, ended.Map, route.Host);
        var destination = post is { } tile && !IsAt(trader.Position, tile) ? post : null;
        return ended.WithUnit(trader with { Destination = destination });
    }

    // Starts an accepted deal: its trader turns towards the home city's trade post at the start of its owner's next walk.
    public static GameState Accept(Ruleset ruleset, GameState state, TradeRoute route, ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Accepted));
        var accepted = state.WithRoute(route with { Deal = route.Deal! with { Accepted = true, AcceptedTurn = state.Turn }, Paused = false });
        return route.Trader is { } traderId && accepted.FindUnit(traderId) is { } trader
            ? accepted.WithUnit(trader with { Destination = PostTile(ruleset, accepted.Map, route.Home) })
            : accepted;
    }

    // The trader stands at the post it walked to (its destination).
    private static GameState Arrive(Ruleset ruleset, GameState state, Unit unit, TradeRoute route, ImmutableArray<GameEvent>.Builder events)
    {
        if (route.IsActive)
        {
            var hostPost = PostTile(ruleset, state.Map, route.Host);
            var next = unit.Destination == hostPost ? PostTile(ruleset, state.Map, route.Home) : hostPost;
            return state.WithUnit(unit with { Destination = next });
        }

        var waiting = state.WithUnit(unit with { Destination = null });
        if (route.Opened)
        {
            return waiting;
        }

        events.Add(new TradeRouteChangedEvent(route.Id, route.Owner, route.HostOwner, route.Home, route.Host, TradeChange.Opened));
        return waiting.WithRoute(route with { Opened = true });
    }

    private static ImmutableArray<(HexCoord Tile, int Cost)> ToPost(ImmutableArray<(HexCoord Tile, int Cost)> path, HexCoord post)
    {
        var steps = path.TakeWhile(step => !IsAt(step.Tile, post)).Count();
        return steps < path.Length ? path[..(steps + 1)] : path;
    }

    private static ImmutableArray<ResourceAmount> Swap(Ruleset ruleset, ImmutableArray<ResourceAmount> stock, IEnumerable<ResourceAmount> give, IEnumerable<ResourceAmount> take) =>
        YieldCalculator.Sum(ruleset, UpkeepCalculator.Subtract(ruleset, stock, give).Concat(take));

    private static City WithStock(City city, ImmutableArray<ResourceAmount> stock) => city with { Stock = stock.IsEmpty ? null : stock };
}
