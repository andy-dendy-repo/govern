namespace Govern.Core.Economy;

/// <summary>What happened to a trade deal (<see cref="Events.TradeDealChangedEvent"/>).</summary>
public enum TradeChange
{
    /// <summary>The trader's owner proposed the deal to the host city.</summary>
    Proposed,

    /// <summary>The side that had to answer offered other terms instead.</summary>
    Countered,

    /// <summary>The terms were accepted (a deal with one's own city is accepted at once); the trader sets off for the host's trade post.</summary>
    Accepted,

    /// <summary>The side that had to answer declined; the deal is gone.</summary>
    Declined,

    /// <summary>The trader reached the host's trade post: the deal is active and swaps goods every turn.</summary>
    Started,

    /// <summary>One side cancelled the deal; its trader goes home.</summary>
    Cancelled,

    /// <summary>The trader was killed, which ends its deal.</summary>
    Broken,

    /// <summary>One of the deal's cities changed hands, which ends it.</summary>
    Ended,
}
