using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Ends the actor's turn: their cities produce, and the next active player moves.
/// </summary>
public sealed record EndTurnCommand : GameCommand;

/// <summary>
/// Ends the actor's turn: their cities produce, then the next active player moves with full movement.
/// </summary>
internal static class EndTurnRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor)
    {
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var produced = state.Cities
            .Where(city => city.Owner == actor)
            .Select(static city => city.Id)
            .Aggregate(state, (current, cityId) => Produce(ruleset, current, current.FindCity(cityId)!, events));

        return new GameUpdate(PassTurn(ruleset, produced, events), events.ToImmutable());
    }

    // Hands the turn to the next active player in turn order; the round number increases when it wraps around.
    private static GameState PassTurn(Ruleset ruleset, GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        var players = state.Players;
        var currentIndex = players.Select(static player => player.Id).ToList().IndexOf(state.CurrentPlayer);

        for (var step = 1; step <= players.Length; step++)
        {
            var next = players[(currentIndex + step) % players.Length];
            if (next.Status != PlayerStatus.Active)
            {
                continue;
            }

            var turn = currentIndex + step >= players.Length ? state.Turn + 1 : state.Turn;
            events.Add(new TurnEndedEvent(state.CurrentPlayer, next.Id, turn));
            return ResetMovement(ruleset, state with { CurrentPlayer = next.Id, Turn = turn }, next.Id);
        }

        return state;
    }

    private static GameState Produce(Ruleset ruleset, GameState state, City city, ImmutableArray<GameEvent>.Builder events)
    {
        var cost = ruleset.Unit(city.Producing).ProductionCost;
        var progress = Math.Min(city.ProductionProgress + ruleset.Cities.ProductionPerTurn, cost);
        if (progress < cost || SpawnTile(ruleset, state, city) is not { } tile)
        {
            return state.WithCity(city with { ProductionProgress = progress });
        }

        var (allocated, id) = state.AllocateId();
        var unit = new Unit(new UnitId(id), city.Owner, city.Producing, tile, MovementLeft: 0);
        events.Add(new UnitCreatedEvent(unit, city.Id));

        var reset = allocated.WithCity(city with { ProductionProgress = 0 });
        return reset with { Units = reset.Units.Add(unit) };
    }

    // The city tile when free, otherwise the first free neighbour a unit could stand on; null when surrounded.
    private static HexCoord? SpawnTile(Ruleset ruleset, GameState state, City city)
    {
        foreach (var coord in city.Position.Neighbors().Prepend(city.Position))
        {
            var ownCityOrNone = state.CityAt(coord) is not { } other || other.Owner == city.Owner;
            if (state.Map.IsPassable(ruleset, coord) && state.UnitAt(coord) is null && ownCityOrNone)
            {
                return coord;
            }
        }

        return null;
    }

    private static GameState ResetMovement(Ruleset ruleset, GameState state, PlayerId player) => state with
    {
        Units = [.. state.Units.Select(unit => unit.Owner == player ? unit with { MovementLeft = ruleset.Unit(unit.Type).Movement } : unit)],
    };
}
