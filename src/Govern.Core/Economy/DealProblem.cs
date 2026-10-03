namespace Govern.Core.Economy;

/// <summary>Why a trade deal's terms are not valid (<see cref="TradeCalculator.CheckDeal"/>).</summary>
public enum DealProblem
{
    /// <summary>One side gives nothing.</summary>
    Empty,

    /// <summary>A resource can't be traded.</summary>
    NotTradable,

    /// <summary>An amount is outside 1 to the largest cap, or a resource is listed twice on one side.</summary>
    BadAmount,

    /// <summary>The home city's market doesn't sell or buy that much.</summary>
    HomeMarket,

    /// <summary>The host city's market doesn't sell or buy that much.</summary>
    HostMarket,
}
