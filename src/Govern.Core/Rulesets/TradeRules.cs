using Govern.Core.Economy;

namespace Govern.Core.Rulesets;

/// <summary>
/// Trading (the ruleset's <c>trade</c> section, G17): cities with the <see cref="Building"/> keep a market, traders open routes
/// between two such cities' trade post tiles, and each city's demand level per tradable resource moves with what it wants and
/// what it offers. A resource is tradable when its <see cref="ResourceDefinition.TradeValue"/> is above 0.
/// </summary>
/// <param name="Building">The building a city needs to trade; its tile is the trade post traders walk between.</param>
/// <param name="RoutesPerCity">How many routes a city may open (it may host any number).</param>
/// <param name="MaxCap">The largest cap a market order may have, per turn.</param>
/// <param name="DemandStepPercent">The most a demand level moves in one turn, in percent of itself.</param>
/// <param name="DemandSmoothing">Added to need plus proposition when sizing the step, so small markets move gently.</param>
/// <param name="MinDemandPercent">The lowest demand level.</param>
/// <param name="MaxDemandPercent">The highest demand level.</param>
public sealed record TradeRules(
    BuildingType Building,
    int RoutesPerCity,
    int MaxCap,
    int DemandStepPercent,
    int DemandSmoothing,
    int MinDemandPercent,
    int MaxDemandPercent)
{
    /// <summary>The demand level every resource starts at, in percent.</summary>
    public const int BaseDemandPercent = 100;
}
