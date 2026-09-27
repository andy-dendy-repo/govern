using System.Collections.Immutable;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Engine;

/// <summary>
/// Runs after every accepted command: eliminates active players without cities and settlers, removes the units of players who
/// left, and finishes the game when at most one active player remains (with two players, anyone leaving ends the game).
/// </summary>
internal static class VictoryRule
{
    public static GameUpdate Apply(GameUpdate update)
    {
        var events = update.Events.ToBuilder();
        var state = RemoveDepartedUnits(Eliminate(update.State, events), events);
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

        return new GameUpdate(state, events.ToImmutable());
    }

    private static GameState Eliminate(GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active && !CanStillPlay(state, player.Id)))
        {
            state = state.WithPlayer(player with { Status = PlayerStatus.Eliminated });
            events.Add(new PlayerEliminatedEvent(player.Id));
        }

        return state;
    }

    private static bool CanStillPlay(GameState state, PlayerId player) =>
        state.Cities.Any(city => city.Owner == player)
        || state.Units.Any(unit => unit.Owner == player && unit.Definition.CanFoundCity);

    private static GameState RemoveDepartedUnits(GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        var departed = state.Players.Where(static player => player.Status != PlayerStatus.Active).Select(static player => player.Id).ToHashSet();
        var removed = state.Units.Where(unit => departed.Contains(unit.Owner)).ToList();
        events.AddRange(removed.Select(static unit => new UnitDestroyedEvent(unit.Id, unit.Owner, unit.Position)));

        return removed.Count == 0 ? state : state with { Units = [.. state.Units.Except(removed)] };
    }
}
