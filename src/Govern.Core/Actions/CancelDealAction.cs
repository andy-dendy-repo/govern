using System.Collections.Immutable;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Cancels a trade deal from either side, whatever its state (G17b): an offer is withdrawn, an agreed or active deal ends. The
/// deal is gone, and its trader walks home to its own trade post, free for the next deal.
/// </summary>
/// <param name="DealId">A deal of one of the actor's traders, or one hosted by one of the actor's cities.</param>
public sealed record CancelDealCommand(TradeDealId DealId) : GameCommand;

/// <summary>Ends a deal (see <see cref="CancelDealCommand"/>).</summary>
internal static class CancelDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, CancelDealCommand command)
    {
        if (state.FindDeal(command.DealId) is not { } deal || (deal.Owner != actor && deal.HostOwner != actor))
        {
            return RuleViolations.DealNotFound;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        return new GameUpdate(TradeRule.EndDeal(ruleset, state, deal, TradeChange.Cancelled, events), events.ToImmutable());
    }
}
