using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Sets what a city's market does with a tradable resource (G17): sell or buy up to a cap each turn, or nothing.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="Resource">A tradable resource.</param>
/// <param name="Side">Sell or buy; <see langword="null"/> removes the order.</param>
/// <param name="Cap">The most the city's deals may give or take of it each turn, 1 to <c>trade.maxCap</c>; ignored without a side.</param>
public sealed record SetMarketOrderCommand(CityId CityId, ResourceType Resource, MarketSide? Side, int Cap = 0) : GameCommand;

/// <summary>
/// Changes a city's market (see <see cref="SetMarketOrderCommand"/>). An order can't drop below what the city's deals already use of it (accepted deals, and its own
/// proposals), so a deal never outgrows its markets; cancel a deal first.
/// </summary>
internal static class SetMarketOrderRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, SetMarketOrderCommand command)
    {
        var city = state.FindCity(command.CityId);
        if (city is null || city.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        if (ruleset.FindResource(command.Resource) is not { IsTradable: true })
        {
            return RuleViolations.NotTradable;
        }

        if (command.Side is { } side && (!Enum.IsDefined(side) || command.Cap < 1 || command.Cap > ruleset.Trade.MaxCap))
        {
            return RuleViolations.CapOutOfRange;
        }

        var deals = TradeRule.Deals(state).ToList();
        foreach (var each in Enum.GetValues<MarketSide>())
        {
            var allowed = command.Side == each ? command.Cap : 0;
            if (TradeCalculator.Committed(deals, city.Id, command.Resource, each) > allowed)
            {
                return RuleViolations.CapBelowDeals;
            }
        }

        var others = city.MarketList.Where(existing => existing.Resource != command.Resource);
        var orders = command.Side is { } newSide ? others.Append(new MarketOrder(command.Resource, newSide, command.Cap)) : others;
        var sorted = ruleset.Resources.SelectMany(resource => orders.Where(existing => existing.Resource == resource.Id)).ToList();
        return new GameUpdate(state.WithCity(city with { Market = sorted.Count == 0 ? null : [.. sorted] }), []);
    }
}
