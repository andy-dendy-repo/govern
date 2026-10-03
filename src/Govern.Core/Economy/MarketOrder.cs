namespace Govern.Core.Economy;

/// <summary>
/// One line of a city's market (G17): the city sells or buys a tradable resource, at most <see cref="Cap"/> a turn over all its
/// accepted deals together.
/// </summary>
/// <param name="Resource">A tradable resource.</param>
/// <param name="Side">Sell or buy.</param>
/// <param name="Cap">The most the city's deals may give (or take) of it each turn, 1 to <see cref="Rulesets.TradeRules.MaxCap"/>.</param>
public sealed record MarketOrder(ResourceType Resource, MarketSide Side, int Cap);
