using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Rulesets;

namespace Govern.Core.Economy;

/// <summary>
/// The arithmetic of trading (G17, G17b), shared by the rules on the state and by the client on the view so both decide the same way:
/// demand levels and values, what a city's deals already use of its market, and whether a deal fits two markets.
/// </summary>
public static class TradeCalculator
{
    /// <summary>A city's demand level for a resource.</summary>
    /// <param name="demands">The city's levels that differ from the base level.</param>
    /// <param name="resource">The resource.</param>
    /// <returns>The level in percent; <see cref="TradeRules.BaseDemandPercent"/> when it is not listed.</returns>
    public static int DemandPercent(IEnumerable<ResourceDemand> demands, ResourceType resource)
    {
        ArgumentNullException.ThrowIfNull(demands);
        return demands.FirstOrDefault(demand => demand.Resource == resource)?.Percent ?? TradeRules.BaseDemandPercent;
    }

    /// <summary>What one unit of a resource is worth in a city: its trade value times the city's demand level.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="demands">The city's demand levels.</param>
    /// <param name="resource">A tradable resource.</param>
    /// <returns>The value in hundredths (a trade value of 2 at 150 % is 300); 0 for a resource that isn't tradable.</returns>
    public static int Value(Ruleset ruleset, IEnumerable<ResourceDemand> demands, ResourceType resource)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return (ruleset.FindResource(resource)?.TradeValue ?? 0) * DemandPercent(demands, resource);
    }

    /// <summary>
    /// How much of a resource a city's agreed deals use of its market: what it gives (selling) or takes (buying) each turn.
    /// Offers still on the table use nothing; both markets are checked again when one is accepted.
    /// </summary>
    /// <param name="deals">Agreed deals (those the city isn't part of are ignored), as home, host and terms.</param>
    /// <param name="city">The city.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="side">Selling or buying.</param>
    /// <returns>The amount per turn.</returns>
    public static int Committed(IEnumerable<(CityId Home, CityId Host, TradeTerms Terms)> deals, CityId city, ResourceType resource, MarketSide side)
    {
        ArgumentNullException.ThrowIfNull(deals);

        var total = 0;
        foreach (var (home, host, deal) in deals)
        {
            if (home != city && host != city)
            {
                continue;
            }

            // The home city gives Gives and takes Receives; the host the other way round.
            var given = home == city ? deal.Gives : deal.Receives;
            var taken = home == city ? deal.Receives : deal.Gives;
            total += YieldCalculator.AmountOf(side == MarketSide.Sell ? given : taken, resource);
        }

        return total;
    }

    /// <summary>A city's market with each order's cap reduced by what its deals already use (<see cref="Committed"/>).</summary>
    /// <param name="market">The city's orders.</param>
    /// <param name="deals">Agreed deals, without the ones being decided.</param>
    /// <param name="city">The city.</param>
    /// <returns>The orders with what is still free of each cap (possibly 0), in the same order.</returns>
    public static ImmutableArray<MarketOrder> FreeMarket(IEnumerable<MarketOrder> market, IEnumerable<(CityId Home, CityId Host, TradeTerms Terms)> deals, CityId city)
    {
        ArgumentNullException.ThrowIfNull(market);
        var agreed = deals.ToList();
        return [.. market.Select(order => order with { Cap = Math.Max(0, order.Cap - Committed(agreed, city, order.Resource, order.Side)) })];
    }

    /// <summary>
    /// Why a deal's terms are not valid: each side gives something, every resource is tradable and listed once per side with an
    /// amount from 1 to <see cref="TradeRules.MaxCap"/>, and both cities' markets have room for it (the home city sells what it
    /// gives and buys what it receives, the host the other way round, within their free caps).
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="homeFree">The home city's free market (<see cref="FreeMarket"/>).</param>
    /// <param name="hostFree">The host city's free market.</param>
    /// <param name="gives">What the home city would give each turn.</param>
    /// <param name="receives">What the host city would give back each turn.</param>
    /// <returns><see langword="null"/> when the terms are valid; otherwise the problem.</returns>
    public static DealProblem? CheckDeal(
        Ruleset ruleset,
        IEnumerable<MarketOrder> homeFree,
        IEnumerable<MarketOrder> hostFree,
        IEnumerable<ResourceAmount> gives,
        IEnumerable<ResourceAmount> receives)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var (given, taken) = (gives.ToList(), receives.ToList());
        if (given.Count == 0 || taken.Count == 0)
        {
            return DealProblem.Empty;
        }

        // A command's JSON may carry null items; they are malformed amounts, not a crash.
        if (given.Concat(taken).Any(static amount => amount is null))
        {
            return DealProblem.BadAmount;
        }

        if (given.Concat(taken).Any(amount => ruleset.FindResource(amount.Resource) is not { IsTradable: true }))
        {
            return DealProblem.NotTradable;
        }

        if (!ValidAmounts(ruleset, given) || !ValidAmounts(ruleset, taken))
        {
            return DealProblem.BadAmount;
        }

        var (home, host) = (homeFree.ToList(), hostFree.ToList());
        if (!Fits(home, MarketSide.Sell, given) || !Fits(home, MarketSide.Buy, taken))
        {
            return DealProblem.HomeMarket;
        }

        return Fits(host, MarketSide.Buy, given) && Fits(host, MarketSide.Sell, taken) ? null : DealProblem.HostMarket;
    }

    /// <summary>
    /// A demand level after a turn: it moves toward the bigger of need and proposition by up to
    /// <see cref="TradeRules.DemandStepPercent"/>, scaled by <c>(need − proposition) / (need + proposition + smoothing)</c>, and stays
    /// within <see cref="TradeRules.MinDemandPercent"/>..<see cref="TradeRules.MaxDemandPercent"/>. Integer maths, truncated
    /// toward zero, so it is the same everywhere.
    /// </summary>
    /// <param name="rules">The ruleset's trade section.</param>
    /// <param name="percent">The level now.</param>
    /// <param name="need">What the city wants and has no deal for, plus upkeep in it the city couldn't pay.</param>
    /// <param name="proposition">What the city offers and nobody takes.</param>
    /// <returns>The new level.</returns>
    public static int NextDemand(TradeRules rules, int percent, int need, int proposition)
    {
        ArgumentNullException.ThrowIfNull(rules);
        // In long, so a huge unpaid upkeep can't overflow; the change is at most the step, so it fits back in an int.
        var change = (long)percent * rules.DemandStepPercent * (need - proposition) / (100L * (need + proposition + rules.DemandSmoothing));
        return (int)Math.Clamp(percent + change, rules.MinDemandPercent, rules.MaxDemandPercent);
    }

    private static bool ValidAmounts(Ruleset ruleset, List<ResourceAmount> amounts) =>
        amounts.All(amount => amount.Amount >= 1 && amount.Amount <= ruleset.Trade.MaxCap)
        && amounts.Select(static amount => amount.Resource).Distinct().Count() == amounts.Count;

    private static bool Fits(List<MarketOrder> market, MarketSide side, List<ResourceAmount> amounts) =>
        amounts.All(amount => market.Any(order => order.Resource == amount.Resource && order.Side == side && order.Cap >= amount.Amount));
}
