using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// The trade commands' checks (G17b) worked out from the viewer's <see cref="PlayerView"/> alone, for the client (the trade
/// counterpart of <see cref="GameEngine"/>'s previews). Each answer equals what <see cref="GameEngine.Execute"/> decides: the view
/// holds every deal the viewer is a side of, their own cities' markets, and every other city they can trade with
/// (<see cref="PlayerView.TradePartners"/>) with its market less the deals the viewer can't see.
/// </summary>
public static class TradePreviewCalculator
{
    /// <summary>A city's market with what its agreed deals use taken off each cap, as the rules see it.</summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="cityId">One of the viewer's cities, or a trade partner.</param>
    /// <param name="except">Deals to leave out (being decided or cancelled); none when <see langword="null"/>.</param>
    /// <returns>The free market; empty for a city the viewer can't trade with.</returns>
    public static ImmutableArray<MarketOrder> FreeMarket(PlayerView view, CityId cityId, IReadOnlySet<TradeDealId>? except = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var market = OwnCity(view, cityId)?.MarketList ?? Partner(view, cityId)?.MarketList ?? [];
        var agreed = view.TradeDealList
            .Where(deal => deal.IsAgreed && except?.Contains(deal.Id) != true)
            .Select(static deal => (deal.Home, deal.Host, deal.Terms));
        return TradeCalculator.FreeMarket(market, agreed, cityId);
    }

    /// <summary>A city's demand levels, as far as the viewer knows them.</summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="cityId">One of the viewer's cities, or a trade partner.</param>
    /// <returns>The levels that differ from the base level (empty when none, or for a city the viewer can't trade with).</returns>
    public static ImmutableArray<ResourceDemand> Demand(PlayerView view, CityId cityId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return OwnCity(view, cityId)?.DemandList ?? Partner(view, cityId)?.DemandList ?? [];
    }

    /// <summary>The cities a trader from <paramref name="homeId"/> can send a deal to: the viewer's other cities with a trade post, then their trade partners.</summary>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="homeId">The trader's home city.</param>
    /// <returns>Each target's id, name and owner, own cities first, each group by id.</returns>
    public static ImmutableArray<(CityId Id, string Name, Core.State.PlayerId Owner)> Targets(PlayerView view, CityId homeId)
    {
        ArgumentNullException.ThrowIfNull(view);
        var own = view.Cities
            .Where(city => city.Owner == view.Viewer && city.Id != homeId && city.TradePost is not null)
            .Select(static city => (city.Id, city.Name, city.Owner));
        var partners = view.TradePartnerList.Where(partner => partner.Id != homeId).Select(static partner => (partner.Id, partner.Name, partner.Owner));
        return [.. own, .. partners];
    }

    /// <summary>Whether one of the viewer's traders can send a deal to a city now.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="traderId">The trader.</param>
    /// <param name="hostId">The city the deal goes to.</param>
    /// <param name="terms">The terms offered.</param>
    /// <returns>
    /// <see langword="null"/> when a <see cref="ProposeDealCommand"/> would be accepted; otherwise the violation it would be
    /// rejected with.
    /// </returns>
    public static RuleViolation? CheckProposeDeal(Ruleset ruleset, PlayerView view, UnitId traderId, CityId hostId, TradeTerms terms)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(terms);
        if (ViewPreviewCalculator.CheckTurn(view) is { } turn)
        {
            return turn;
        }

        if (CheckTrader(ruleset, view, traderId) is { } violation)
        {
            return violation;
        }

        var homeId = view.FindOwnUnit(traderId)!.HomeCity!.Value;
        if (hostId == homeId || (OwnCity(view, hostId) is not { TradePost: not null } && Partner(view, hostId) is null))
        {
            return RuleViolations.NoTradePartner;
        }

        return CheckTerms(ruleset, view, (homeId, hostId), Normalize(ruleset, terms), null);
    }

    /// <summary>Whether the viewer can answer the offer on a deal now: accept it (after cancelling the given deals) or counter it with other terms.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The viewer's current view.</param>
    /// <param name="dealId">The deal whose offer waits for the viewer.</param>
    /// <param name="counter">The terms of a counter-offer; <see langword="null"/> to check accepting.</param>
    /// <param name="cancelDeals">With an accept: agreed deals of the answering city to cancel first.</param>
    /// <returns>
    /// <see langword="null"/> when the <see cref="AnswerDealCommand"/> would be accepted; otherwise the violation it would be
    /// rejected with. Declining needs only the first two checks (the deal is the viewer's to answer).
    /// </returns>
    public static RuleViolation? CheckAnswerDeal(Ruleset ruleset, PlayerView view, TradeDealId dealId, TradeTerms? counter, IEnumerable<TradeDealId> cancelDeals)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(cancelDeals);
        if (ViewPreviewCalculator.CheckTurn(view) is { } turn)
        {
            return turn;
        }

        if (view.TradeDealList.FirstOrDefault(deal => deal.Id == dealId) is not { } deal)
        {
            return RuleViolations.DealNotFound;
        }

        if (deal.Answerer != view.Viewer)
        {
            return RuleViolations.NoProposal;
        }

        if (counter is not null)
        {
            return CheckTerms(ruleset, view, (deal.Home, deal.Host), Normalize(ruleset, counter), null);
        }

        var city = deal.AnsweringCity!.Value;
        var cancelled = cancelDeals.ToHashSet();
        foreach (var cancelId in cancelled)
        {
            if (view.TradeDealList.FirstOrDefault(other => other.Id == cancelId) is not { IsAgreed: true } other || other.Id == deal.Id
                || (other.Home != city && other.Host != city))
            {
                return RuleViolations.CannotCancelDeal;
            }
        }

        return CheckTerms(ruleset, view, (deal.Home, deal.Host), deal.Terms, cancelled);
    }

    // The trader's own checks, in the rule's order: the viewer's, able to trade, free, with a home city that has a trade post.
    private static RuleViolation? CheckTrader(Ruleset ruleset, PlayerView view, UnitId traderId)
    {
        if (view.FindOwnUnit(traderId) is not { } trader)
        {
            return RuleViolations.UnitNotFound;
        }

        if (!ruleset.Unit(trader.Type).CanTrade)
        {
            return RuleViolations.CannotTrade;
        }

        if (view.TradeDealList.Any(deal => deal.Trader == trader.Id))
        {
            return RuleViolations.TraderHasDeal;
        }

        if (trader.HomeCity is not { } homeId || OwnCity(view, homeId) is not { } home)
        {
            return RuleViolations.NoHomeCity;
        }

        return home.TradePost is null ? RuleViolations.NoTradePost : null;
    }

    private static RuleViolation? CheckTerms(Ruleset ruleset, PlayerView view, (CityId Home, CityId Host) cities, TradeTerms terms, IReadOnlySet<TradeDealId>? except) =>
        TradeCalculator.CheckDeal(ruleset, FreeMarket(view, cities.Home, except), FreeMarket(view, cities.Host, except), terms.Gives, terms.Receives) is { } problem
            ? TradeRule.Violation(problem)
            : null;

    // As the rule turns a command's lists into terms (TradeRule.Terms).
    private static TradeTerms Normalize(Ruleset ruleset, TradeTerms terms) => TradeRule.Terms(ruleset, terms.Gives, terms.Receives);

    private static CityView? OwnCity(PlayerView view, CityId id) => view.Cities.FirstOrDefault(city => city.Id == id && city.Owner == view.Viewer);

    private static TradePartnerView? Partner(PlayerView view, CityId id) => view.TradePartnerList.FirstOrDefault(partner => partner.Id == id);
}
