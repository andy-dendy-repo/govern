using Govern.Core.Economy;

namespace Govern.Core.Rulesets;

/// <summary>
/// City territory (the ruleset's <c>territory</c> section): the tiles a city owns, gathers from and builds on.
/// </summary>
/// <param name="InitialRadius">A new city claims the unowned tiles up to this distance (its own tile included).</param>
/// <param name="MaxRadius">A city never owns a tile further away than this.</param>
/// <param name="GrowthResource">The resource a city spends to claim one more tile.</param>
/// <param name="TileCost">The price of the first tile a city claims after founding.</param>
/// <param name="TileCostIncrease">How much each tile already claimed after founding adds to the price of the next.</param>
public sealed record TerritoryRules(int InitialRadius, int MaxRadius, ResourceType GrowthResource, int TileCost, int TileCostIncrease)
{
    /// <summary>The price of the next tile.</summary>
    /// <param name="tilesGained">How many tiles the city has claimed since it was founded.</param>
    /// <returns><see cref="TileCost"/> plus <see cref="TileCostIncrease"/> for each tile already gained.</returns>
    public int NextTileCost(int tilesGained) => TileCost + (TileCostIncrease * tilesGained);
}
