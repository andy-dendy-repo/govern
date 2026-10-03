namespace Govern.Core.Economy;

/// <summary>What happened to a trade route (<see cref="Events.TradeRouteChangedEvent"/>).</summary>
public enum TradeChange
{
    /// <summary>The trader reached the host's trade post: the route exists.</summary>
    Opened,

    /// <summary>The route's owner proposed a deal to the host.</summary>
    Proposed,

    /// <summary>The host accepted the deal (a deal with one's own city is accepted at once).</summary>
    Accepted,

    /// <summary>The host declined the proposal.</summary>
    Declined,

    /// <summary>One side cancelled the deal; the route stays.</summary>
    Cancelled,

    /// <summary>The route's trader was killed, which breaks its deal; the route stays.</summary>
    Broken,

    /// <summary>The route ended: its owner closed it, or one of its cities changed hands.</summary>
    Closed,
}
