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

// Trading (G17, reworked in G17b, the owner's model): each trader carries one deal from its home city to a host city. Once both
// sides agree, the trader walks to the host's trade post, the deal turns active when it gets there, and the trader shuttles
// between the two posts while the goods swap at the end of the host owner's turn; each city's demand levels move with what it
// wants and offers. The actions (Actions/*Deal*, SetMarketOrder) make and settle deals; this rule runs the parts that happen on
// their own.
internal static class TradeRule
{
    // The trade commands' share of CommandDispatcher.Dispatch; null for any other command.
    public static CommandResult? Dispatch(Ruleset ruleset, GameState state, PlayerId actor, GameCommand command) => command switch
    {
        SetMarketOrderCommand market => SetMarketOrderRule.Apply(ruleset, state, actor, market),
        ProposeDealCommand propose => ProposeDealRule.Apply(ruleset, state, actor, propose),
        AnswerDealCommand answer => AnswerDealRule.Apply(ruleset, state, actor, answer),
        CancelDealCommand cancel => CancelDealRule.Apply(ruleset, state, actor, cancel),
        _ => null,
    };

    // The tile of a city's trade post (the ruleset's trade building standing on its territory); null when it has none. A game
    // saved before buildings had sites has a trade post with no tile, which can't trade.
    public static HexCoord? PostTile(Ruleset ruleset, GameMap map, CityId city) =>
        map.Tiles.FirstOrDefault(tile => tile.Territory == city && tile.Building is { } building && ruleset.IsKindOf(building, ruleset.Trade.Building))?.Coord;

    // The agreed deals' terms, for TradeCalculator, except the given ones (being decided or cancelled). Offers still on the
    // table use no market: both markets are checked again when they are accepted.
    public static IEnumerable<(CityId Home, CityId Host, TradeTerms Terms)> AgreedTerms(GameState state, IReadOnlySet<TradeDealId>? except = null) =>
        state.TradeDealList
            .Where(deal => deal.IsAgreed && except?.Contains(deal.Id) != true)
            .Select(static deal => (deal.Home, deal.Host, deal.Terms));

    // A city's market with what its agreed deals already use taken off each cap.
    public static ImmutableArray<MarketOrder> FreeMarket(GameState state, City city, IReadOnlySet<TradeDealId>? except = null) =>
        TradeCalculator.FreeMarket(city.MarketList, AgreedTerms(state, except), city.Id);

    // The deal a trader carries, if any.
    public static TradeDeal? DealOf(GameState state, UnitId trader) => state.TradeDealList.FirstOrDefault(deal => deal.Trader == trader);

    // How many traders call a city home.
    public static int TradersOf(Ruleset ruleset, GameState state, CityId city) =>
        state.Units.Count(unit => unit.HomeCity == city && ruleset.Unit(unit.Type).CanTrade);

    // Why a deal's terms between a home and a host city don't fit their free markets (the given deals left out); null when they fit.
    public static RuleViolation? CheckTerms(Ruleset ruleset, GameState state, (CityId Home, CityId Host) cities, TradeTerms terms, IReadOnlySet<TradeDealId>? except = null) =>
        TradeCalculator.CheckDeal(ruleset, FreeMarket(state, state.FindCity(cities.Home)!, except), FreeMarket(state, state.FindCity(cities.Host)!, except), terms.Gives, terms.Receives) is { } problem
            ? Violation(problem)
            : null;

    // The rule violation for each way a deal's terms can be wrong.
    public static RuleViolation Violation(DealProblem problem) => problem switch
    {
        DealProblem.Empty => RuleViolations.EmptyDeal,
        DealProblem.NotTradable => RuleViolations.NotTradable,
        DealProblem.BadAmount => RuleViolations.BadTradeAmount,
        DealProblem.HomeMarket => RuleViolations.HomeMarketFull,
        _ => RuleViolations.HostMarketFull,
    };

    // Terms from a command's lists (a missing list is empty), each side summed in the ruleset's order.
    public static TradeTerms Terms(Ruleset ruleset, ImmutableArray<ResourceAmount> gives, ImmutableArray<ResourceAmount> receives) =>
        new(Normalize(ruleset, gives), Normalize(ruleset, receives));

    // Both sides agreed: the trader drops any job or order and sets off for the host's trade post, at once when its owner
    // accepted on their own turn (with the movement it has left), otherwise on its owner's next turn.
    public static GameState Accept(Ruleset ruleset, GameState state, PlayerId actor, TradeDeal deal, ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(Changed(deal, TradeChange.Accepted));
        var agreed = deal with { Status = TradeDealStatus.Travelling, Paused = false };
        var next = state.WithDeal(agreed);
        if (next.FindUnit(deal.Trader) is not { } trader)
        {
            return next;
        }

        var sent = next.WithUnit(trader with { DealId = deal.Id, Destination = PostTile(ruleset, next.Map, deal.Host), Job = null, Exploring = false });
        return actor == deal.Owner ? Walk(ruleset, sent, sent.FindUnit(trader.Id)!, events) : sent;
    }

    // A trader's walk on an agreed deal (AutoMoveRule.Walk hands it over): towards its destination, a trade post, by the cheapest
    // known way, through units and cities and without stopping for enemies it sights. On the host's post the deal turns active
    // and the trader turns round; from then on it turns round at each post. With no known way, the deal pauses until there is one.
    public static GameState Walk(Ruleset ruleset, GameState state, Unit unit, ImmutableArray<GameEvent>.Builder events)
    {
        if (unit.DealId is not { } dealId || state.FindDeal(dealId) is not { IsAgreed: true } deal || deal.Trader != unit.Id)
        {
            return state.WithUnit(unit with { DealId = null, Destination = null });
        }

        if (unit.Destination is not { } target)
        {
            return state;
        }

        if (unit.Position == target)
        {
            return Arrive(ruleset, state, unit, deal, events);
        }

        if (AutoMoveRule.Plan(ruleset, state, unit, target) is not { } path)
        {
            return deal.Paused ? state : state.WithDeal(deal with { Paused = true });
        }

        var (walkedState, walker, walked, _) = AutoMoveRule.TakeSteps(ruleset, state, unit, path, stopOnSighting: false);
        if (walked.Length > 0)
        {
            events.Add(new UnitMovedEvent(walker.Id, walker.Owner, walker.Type, walked));
        }

        var moving = deal.Paused ? walkedState.WithDeal(deal with { Paused = false }) : walkedState;
        return walker.Position == target ? Arrive(ruleset, moving, walker, moving.FindDeal(dealId)!, events) : moving;
    }

    // Swaps the goods of every active deal hosted by one of the actor's cities, oldest deal first: the home city gives its part
    // and the host its part, or, when either lacks its part, nothing moves this turn. A paused deal swaps nothing.
    public static GameState Exchange(Ruleset ruleset, GameState state, PlayerId actor, ImmutableArray<GameEvent>.Builder events)
    {
        var deals = state.TradeDealList
            .Where(deal => deal.IsSwapping && deal.HostOwner == actor)
            .OrderBy(static deal => deal.ActiveTurn)
            .ThenBy(static deal => deal.Id.Value)
            .ToList();

        foreach (var deal in deals)
        {
            if (state.FindCity(deal.Home) is not { } home || state.FindCity(deal.Host) is not { } host)
            {
                continue;
            }

            var terms = deal.Terms;
            var homeShort = !UpkeepCalculator.Missing(ruleset, home.StockList, terms.Gives).IsEmpty;
            var hostShort = !UpkeepCalculator.Missing(ruleset, host.StockList, terms.Receives).IsEmpty;
            events.Add(new TradeExchangedEvent(deal.Id, deal.Owner, deal.HostOwner, home.Id, host.Id, terms.Gives, terms.Receives, homeShort, hostShort));
            if (homeShort || hostShort)
            {
                continue;
            }

            // A deal with one's own city (home == host is impossible) moves goods between two stocks of one player.
            state = state.WithCity(WithStock(home, Swap(ruleset, home.StockList, terms.Gives, terms.Receives)));
            host = state.FindCity(deal.Host)!;
            state = state.WithCity(WithStock(host, Swap(ruleset, host.StockList, terms.Receives, terms.Gives)));
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

    // After every command: a deal whose home or host city changed hands ends, a deal whose trader died breaks, a unit left
    // pointing at a deal it doesn't serve is freed, and a trader whose home city was lost (or that has none, from a game saved
    // before homes) moves to its owner's nearest city, one with a trade post first.
    public static GameUpdate Reconcile(Ruleset ruleset, GameUpdate update)
    {
        var state = update.State;
        var homeless = state.Units.Where(unit => NeedsHome(ruleset, state, unit)).ToList();
        if (state.TradeDealList.IsEmpty && homeless.Count == 0 && !state.Units.Any(static unit => unit.DealId is not null))
        {
            return update;
        }

        var events = update.Events.ToBuilder();
        foreach (var deal in state.TradeDealList)
        {
            var home = state.FindCity(deal.Home);
            var host = state.FindCity(deal.Host);
            if (home?.Owner != deal.Owner || host?.Owner != deal.HostOwner)
            {
                state = state.WithoutDeal(deal.Id);
                events.Add(Changed(deal, TradeChange.Ended));
            }
            else if (state.FindUnit(deal.Trader) is null)
            {
                state = state.WithoutDeal(deal.Id);
                events.Add(Changed(deal, TradeChange.Broken));
            }
        }

        var stray = state.Units.Where(unit => unit.DealId is { } id && state.FindDeal(id)?.Trader != unit.Id).ToList();
        state = stray.Aggregate(state, static (current, unit) => current.WithUnit(unit with { DealId = null, Destination = null }));
        foreach (var unit in homeless)
        {
            var current = state.FindUnit(unit.Id)!;
            var home = NearestHome(ruleset, state, current);
            if (home != current.HomeCity)
            {
                state = state.WithUnit(current with { HomeCity = home });
            }
        }

        return new GameUpdate(state, events.ToImmutable());
    }

    // Ends a deal (cancelled by either side): it is gone, and its trader, if alive, walks home to its own trade post.
    public static GameState EndDeal(Ruleset ruleset, GameState state, TradeDeal deal, TradeChange change, ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(Changed(deal, change));
        var ended = state.WithoutDeal(deal.Id);
        if (ended.FindUnit(deal.Trader) is not { } trader)
        {
            return ended;
        }

        var post = trader.HomeCity is { } homeCity ? PostTile(ruleset, ended.Map, homeCity) : null;
        var destination = post is { } tile && trader.Position != tile ? post : null;
        return ended.WithUnit(trader with { DealId = null, Destination = destination });
    }

    public static TradeDealChangedEvent Changed(TradeDeal deal, TradeChange change) =>
        new(deal.Id, deal.Owner, deal.HostOwner, deal.Home, deal.Host, change);

    // The trader stands on the post it walked to (its destination).
    private static GameState Arrive(Ruleset ruleset, GameState state, Unit unit, TradeDeal deal, ImmutableArray<GameEvent>.Builder events)
    {
        var hostPost = PostTile(ruleset, state.Map, deal.Host);
        var homePost = PostTile(ruleset, state.Map, deal.Home);
        if (deal.Status == TradeDealStatus.Travelling)
        {
            events.Add(Changed(deal, TradeChange.Started));
            state = state.WithDeal(deal with { Status = TradeDealStatus.Active, ActiveTurn = state.Turn, Paused = false });
            return state.WithUnit(unit with { Destination = homePost });
        }

        return state.WithUnit(unit with { Destination = unit.Destination == hostPost ? homePost : hostPost });
    }

    // A trader needs a home when it has none, or its home city is gone or no longer its owner's.
    private static bool NeedsHome(Ruleset ruleset, GameState state, Unit unit) =>
        ruleset.Unit(unit.Type).CanTrade && (unit.HomeCity is not { } home || state.FindCity(home)?.Owner != unit.Owner);

    // The owner's nearest city to the trader, cities with a trade post first, ties by id; null when the owner has no city.
    private static CityId? NearestHome(Ruleset ruleset, GameState state, Unit trader) => state.Cities
        .Where(city => city.Owner == trader.Owner)
        .OrderBy(city => PostTile(ruleset, state.Map, city.Id) is null ? 1 : 0)
        .ThenBy(city => city.Position.DistanceTo(trader.Position))
        .ThenBy(static city => city.Id.Value)
        .Select(static city => (CityId?)city.Id)
        .FirstOrDefault();

    private static ImmutableArray<ResourceAmount> Normalize(Ruleset ruleset, ImmutableArray<ResourceAmount> amounts)
    {
        var list = amounts.IsDefault ? [] : amounts;
        // A command's JSON may carry null items, amounts below 1 or a resource twice; TradeCalculator.CheckDeal rejects those, so keep them as sent.
        return list.Any(static amount => amount is null || amount.Amount <= 0) || list.Select(static amount => amount.Resource).Distinct().Count() != list.Length
            ? list
            : YieldCalculator.Sum(ruleset, list);
    }

    private static ImmutableArray<ResourceAmount> Swap(Ruleset ruleset, ImmutableArray<ResourceAmount> stock, IEnumerable<ResourceAmount> give, IEnumerable<ResourceAmount> take) =>
        YieldCalculator.Sum(ruleset, UpkeepCalculator.Subtract(ruleset, stock, give).Concat(take));

    private static City WithStock(City city, ImmutableArray<ResourceAmount> stock) => city with { Stock = stock.IsEmpty ? null : stock };
}
