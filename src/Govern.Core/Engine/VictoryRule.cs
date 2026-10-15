using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Engine;

/// <summary>
/// Runs after every accepted command: eliminates active players without cities and settlers, removes the units of players who
/// left, and finishes the game when at most one active player remains (with two players, anyone leaving ends the game). When the
/// game goes on but the player whose turn it is has left (resigned on their own turn), the turn passes to the next active player.
/// A departed player's cities stay where they are, undefended, for the others to capture, and the deals they hosted end (the
/// deals of their own traders break with the traders, in <see cref="TradeRule.Reconcile"/>).
/// </summary>
internal static class VictoryRule
{
    public static GameUpdate Apply(Ruleset ruleset, GameUpdate update)
    {
        var events = update.Events.ToBuilder();
        var state = RemoveDepartedUnits(Eliminate(ruleset, update.State, events), events);
        var active = state.Players.Where(static player => player.Status == PlayerStatus.Active).ToList();

        if (active.Count <= 1)
        {
            var winner = active.Count == 1 ? active[0].Id : (PlayerId?)null;
            if (winner is { } id)
            {
                events.Add(new GameWonEvent(id));
            }

            state = state with { Status = GameStatus.Finished, Winner = winner };
        }
        else
        {
            state = EndHostedDeals(ruleset, state, events);
            if (state.FindPlayer(state.CurrentPlayer) is not { Status: PlayerStatus.Active })
            {
                state = EndTurnRule.PassTurn(ruleset, state, events);
            }
        }

        return new GameUpdate(state, events.ToImmutable());
    }

    private static GameState Eliminate(Ruleset ruleset, GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active && !CanStillPlay(ruleset, state, player.Id)))
        {
            state = state.WithPlayer(player with { Status = PlayerStatus.Eliminated });
            events.Add(new PlayerEliminatedEvent(player.Id));
        }

        return state;
    }

    private static bool CanStillPlay(Ruleset ruleset, GameState state, PlayerId player) =>
        state.Cities.Any(city => city.Owner == player)
        || state.Units.Any(unit => unit.Owner == player && ruleset.Unit(unit.Type).CanFoundCity);

    private static GameState EndHostedDeals(Ruleset ruleset, GameState state, ImmutableArray<GameEvent>.Builder events) => state.TradeDealList
        .Where(deal => state.FindPlayer(deal.HostOwner) is not { Status: PlayerStatus.Active })
        .Aggregate(state, (current, deal) => TradeRule.EndDeal(ruleset, current, deal, TradeChange.Ended, events));

    private static GameState RemoveDepartedUnits(GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        var departed = state.Players.Where(static player => player.Status != PlayerStatus.Active).Select(static player => player.Id).ToHashSet();
        var removed = state.Units.Where(unit => departed.Contains(unit.Owner)).ToList();
        events.AddRange(removed.Select(static unit => new UnitDestroyedEvent(unit.Id, unit.Owner, unit.Type, unit.Position)));

        return removed.Count == 0 ? state : state with { Units = [.. state.Units.Except(removed)] };
    }
}
