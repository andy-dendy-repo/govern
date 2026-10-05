using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Answers the offer on a trade deal that waits for the actor (G17b): the host city's owner answers a proposal, the trader's
/// owner a counter-offer. Accepting can first cancel agreed deals of the answering city to make room in its market; countering
/// puts other terms back to the other side, who answers in turn.
/// </summary>
/// <param name="DealId">The deal whose offer waits for the actor.</param>
/// <param name="Answer">Accept, decline or counter.</param>
/// <param name="Gives">With a counter: what the home city would give each turn instead.</param>
/// <param name="Receives">With a counter: what the host city would give back each turn instead.</param>
/// <param name="CancelDeals">With an accept: agreed deals of the answering city to cancel first (it is their home or host).</param>
public sealed record AnswerDealCommand(
    TradeDealId DealId,
    DealAnswer Answer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceAmount>? Gives = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<ResourceAmount>? Receives = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImmutableArray<TradeDealId>? CancelDeals = null) : GameCommand;

/// <summary>Accepts, declines or counters an offer (see <see cref="AnswerDealCommand"/>); the whole answer is rejected if the terms don't fit.</summary>
internal static class AnswerDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, AnswerDealCommand command)
    {
        if (state.FindDeal(command.DealId) is not { } deal || (deal.Owner != actor && deal.HostOwner != actor))
        {
            return RuleViolations.DealNotFound;
        }

        if (deal.Answerer != actor)
        {
            return RuleViolations.NoProposal;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        switch (command.Answer)
        {
            case DealAnswer.Decline:
                events.Add(TradeRule.Changed(deal, TradeChange.Declined));
                return new GameUpdate(state.WithoutDeal(deal.Id), events.ToImmutable());
            case DealAnswer.Counter:
                return Counter(ruleset, state, deal, command, events);
            case DealAnswer.Accept:
                return Accept(ruleset, state, deal, command.CancelDeals ?? [], events);
            default:
                return RuleViolations.UnknownAnswer;
        }
    }

    // Other terms go back to the other side; they must fit both free markets now.
    private static CommandResult Counter(Ruleset ruleset, GameState state, TradeDeal deal, AnswerDealCommand command, ImmutableArray<GameEvent>.Builder events)
    {
        var terms = TradeRule.Terms(ruleset, command.Gives ?? [], command.Receives ?? []);
        if (TradeRule.CheckTerms(ruleset, state, (deal.Home, deal.Host), terms) is { } violation)
        {
            return violation;
        }

        var status = deal.Status == TradeDealStatus.Proposed ? TradeDealStatus.Countered : TradeDealStatus.Proposed;
        var countered = deal with { Terms = terms, Status = status };
        events.Add(TradeRule.Changed(countered, TradeChange.Countered));
        return new GameUpdate(state.WithDeal(countered), events.ToImmutable());
    }

    // The actor is the deal's answerer (checked by Apply).
    private static CommandResult Accept(Ruleset ruleset, GameState state, TradeDeal deal, ImmutableArray<TradeDealId> cancelIds, ImmutableArray<GameEvent>.Builder events)
    {
        var city = deal.AnsweringCity!.Value;
        foreach (var cancelId in cancelIds.Distinct())
        {
            if (state.FindDeal(cancelId) is not { IsAgreed: true } cancelled || cancelled.Id == deal.Id || (cancelled.Home != city && cancelled.Host != city))
            {
                return RuleViolations.CannotCancelDeal;
            }

            state = TradeRule.EndDeal(ruleset, state, cancelled, TradeChange.Cancelled, events);
        }

        if (TradeRule.CheckTerms(ruleset, state, (deal.Home, deal.Host), deal.Terms) is { } violation)
        {
            return violation;
        }

        return new GameUpdate(TradeRule.Accept(ruleset, state, deal.Answerer!.Value, deal, events), events.ToImmutable());
    }
}
