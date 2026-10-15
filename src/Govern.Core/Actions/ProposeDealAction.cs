using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Sends a trader's deal from its home city to another city (G17b): every turn the deal is active the home city gives
/// <see cref="Gives"/> and the host city gives <see cref="Receives"/> back. The trader can be anywhere; it sets off for the host's
/// trade post once the deal is accepted. A deal with one's own city is accepted at once; otherwise the host's owner answers it
/// (accept, decline or counter).
/// </summary>
/// <param name="TraderId">The actor's trader, carrying no deal; its home city needs a trade post.</param>
/// <param name="HostId">Another city with a trade post: the actor's own, or one whose trade post the actor has seen.</param>
/// <param name="Gives">What the home city gives each turn: tradable resources it sells and the host buys, within both free caps.</param>
/// <param name="Receives">What the host gives back each turn: tradable resources it sells and the home city buys, within both free caps.</param>
public sealed record ProposeDealCommand(UnitId TraderId, CityId HostId, ImmutableArray<ResourceAmount> Gives, ImmutableArray<ResourceAmount> Receives) : GameCommand;

/// <summary>Puts a trader's deal on the table (see <see cref="ProposeDealCommand"/>).</summary>
internal static class ProposeDealRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, ProposeDealCommand command)
    {
        if (Check(ruleset, state, actor, command) is { } violation)
        {
            return violation;
        }

        var trader = state.FindUnit(command.TraderId)!;
        var host = state.FindCity(command.HostId)!;
        var (allocated, id) = state.AllocateId();
        var deal = new TradeDeal(
            new TradeDealId(id), actor, trader.HomeCity!.Value, host.Id, host.Owner, trader.Id,
            TradeRule.Terms(ruleset, command.Gives, command.Receives), TradeDealStatus.Proposed);
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        events.Add(TradeRule.Changed(deal, TradeChange.Proposed));
        var next = allocated.WithDeal(deal);
        return new GameUpdate(host.Owner == actor ? TradeRule.Accept(ruleset, next, actor, deal, events) : next, events.ToImmutable());
    }

    private static RuleViolation? Check(Ruleset ruleset, GameState state, PlayerId actor, ProposeDealCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.TraderId, out var trader, out var error))
        {
            return error;
        }

        if (!ruleset.Unit(trader.Type).CanTrade)
        {
            return RuleViolations.CannotTrade;
        }

        if (TradeRule.DealOf(state, trader.Id) is not null)
        {
            return RuleViolations.TraderHasDeal;
        }

        if (trader.HomeCity is not { } homeId || state.FindCity(homeId) is not { } home || home.Owner != actor)
        {
            return RuleViolations.NoHomeCity;
        }

        if (TradeRule.PostTile(ruleset, state.Map, home.Id) is null)
        {
            return RuleViolations.NoTradePost;
        }

        if (!IsPartner(ruleset, state, actor, home.Id, command.HostId))
        {
            return RuleViolations.NoTradePartner;
        }

        var terms = TradeRule.Terms(ruleset, command.Gives, command.Receives);
        return TradeRule.CheckTerms(ruleset, state, (home.Id, command.HostId), terms);
    }

    // Another city with a trade post the actor knows of: their own, or one whose post they have seen (Player.KnownPosts; a city
    // they never saw the post of reads like no partner at all), of a player still in the game (a player who is out trades with
    // nobody).
    private static bool IsPartner(Ruleset ruleset, GameState state, PlayerId actor, CityId homeId, CityId hostId) =>
        state.FindCity(hostId) is { } host && host.Id != homeId && TradeRule.PostTile(ruleset, state.Map, host.Id) is not null
        && (host.Owner == actor || state.FindPlayer(actor)!.KnownPostList.Contains(host.Id))
        && state.FindPlayer(host.Owner) is { Status: PlayerStatus.Active };
}
