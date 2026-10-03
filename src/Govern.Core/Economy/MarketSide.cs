namespace Govern.Core.Economy;

/// <summary>Whether a city's market sells or buys a resource (<see cref="MarketOrder"/>).</summary>
public enum MarketSide
{
    /// <summary>The city gives the resource in its deals, up to the order's cap.</summary>
    Sell,

    /// <summary>The city takes the resource in its deals, up to the order's cap.</summary>
    Buy,
}
