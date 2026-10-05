namespace Govern.Core.Economy;

/// <summary>How a player answers the offer on a trade deal (<see cref="Actions.AnswerDealCommand"/>).</summary>
public enum DealAnswer
{
    /// <summary>Agree to the terms on the table: the trader sets off for the host's trade post.</summary>
    Accept,

    /// <summary>Refuse the terms: the deal is gone and the trader is free.</summary>
    Decline,

    /// <summary>Offer other terms back to the other side.</summary>
    Counter,
}
