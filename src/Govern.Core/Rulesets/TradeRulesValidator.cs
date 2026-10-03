using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The trade checks of RulesetValidator (via EconomyRulesValidator): resources' trade values and the trade section. Problems go
// into the same list, with JSON paths.
internal static class TradeRulesValidator
{
    private const int MaxRoutesPerCity = 10;
    private const int MaxCap = 100;
    private const int MaxSmoothing = 1000;
    private const int MaxDemandPercent = 1000;

    public static void Validate(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Resources.Length; index++)
        {
            var resource = document.Resources[index];
            RulesetValidator.InRange(resource.TradeValue, 0, EconomyRulesValidator.MaxYield, $"$.resources[{index}].tradeValue", problems);

            // Trade happens just before territory growth; keeping the growth resource out of it keeps the city panel's price of
            // the next tile exact.
            if (resource.IsTradable && resource.Id == document.Territory.GrowthResource)
            {
                problems.Add(new($"$.resources[{index}].tradeValue", $"Must be 0: '{resource.Id.Id}' pays for territory."));
            }
        }

        if (!document.Resources.Any(static resource => resource.IsTradable))
        {
            problems.Add(new("$.resources", "At least one resource must be tradable (a tradeValue of 1 or more)."));
        }

        // A trader on a route takes no other order, so the trade ability can't share a unit with another one.
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            if (unit.CanTrade && unit.Abilities.Any(static ability => ability != UnitAbility.Trade))
            {
                problems.Add(new($"$.units[{index}].abilities", "A unit with 'trade' can have no other ability."));
            }
        }

        var trade = document.Trade;
        if (document.Buildings.All(building => building.Id != trade.Building))
        {
            problems.Add(new("$.trade.building", $"The building '{trade.Building.Id}' does not exist."));
        }

        RulesetValidator.InRange(trade.RoutesPerCity, 1, MaxRoutesPerCity, "$.trade.routesPerCity", problems);
        RulesetValidator.InRange(trade.MaxCap, 1, MaxCap, "$.trade.maxCap", problems);
        RulesetValidator.InRange(trade.DemandStepPercent, 1, 100, "$.trade.demandStepPercent", problems);
        RulesetValidator.InRange(trade.DemandSmoothing, 1, MaxSmoothing, "$.trade.demandSmoothing", problems);
        RulesetValidator.InRange(trade.MinDemandPercent, 1, TradeRules.BaseDemandPercent, "$.trade.minDemandPercent", problems);
        RulesetValidator.InRange(trade.MaxDemandPercent, TradeRules.BaseDemandPercent, MaxDemandPercent, "$.trade.maxDemandPercent", problems);
    }
}
