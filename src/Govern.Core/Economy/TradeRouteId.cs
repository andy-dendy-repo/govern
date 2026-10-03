namespace Govern.Core.Economy;

/// <summary>
/// Identifier of a trade route, unique within one game among routes, units and cities, and drawn at random from the game's
/// generator, so it reveals nothing about how many were opened.
/// </summary>
/// <param name="Value">Positive number.</param>
public readonly record struct TradeRouteId(int Value);
