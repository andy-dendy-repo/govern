namespace Govern.Core.Economy;

/// <summary>Where a trade deal stands (G17b).</summary>
public enum TradeDealStatus
{
    /// <summary>The trader's owner offered the terms; the host city's owner answers (accept, decline or counter).</summary>
    Proposed,

    /// <summary>The host city's owner offered other terms; the trader's owner answers (accept, decline or counter again).</summary>
    Countered,

    /// <summary>Both agreed; the trader walks to the host's trade post, and the deal starts when it gets there.</summary>
    Travelling,

    /// <summary>The trader reached the host's trade post: it shuttles between the two posts and the goods swap every turn.</summary>
    Active,
}
