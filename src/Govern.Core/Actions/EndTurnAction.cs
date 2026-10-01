using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Ends the actor's turn: their resting units heal, their units' building jobs advance, their cities yield resources and
/// produce, and the next active player moves.
/// </summary>
public sealed record EndTurnCommand : GameCommand;

/// <summary>
/// Ends the actor's turn: their hurt units that neither moved, attacked nor worked heal, their units' jobs advance (see
/// <see cref="ConstructionRule"/>), their cities add their yields to the stockpile and produce (except a city whose production a
/// job used), then the next active player moves with full movement.
/// </summary>
internal static class EndTurnRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor)
    {
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var (built, usedCities) = ConstructionRule.AdvanceJobs(ruleset, Heal(ruleset, state, actor), actor, events);
        var produced = built.Cities
            .Where(city => city.Owner == actor && !usedCities.Contains(city.Id))
            .Select(static city => city.Id)
            .Aggregate(ConstructionRule.CollectYields(ruleset, built, actor), (current, cityId) => Produce(ruleset, current, current.FindCity(cityId)!, events));

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
        var perTurn = WorkArea.ProductionPerTurn(ruleset, city.BuildingList);
        if (city.ProducingBuilding is { } building)
        {
            return ProduceBuilding(ruleset, state, city, building, events);
        }

        var cost = ruleset.Unit(city.Producing).ProductionCost;
        var progress = Math.Min(city.ProductionProgress + perTurn, cost);
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

    // A finished building joins the city, which goes back to producing its unit from scratch.
    private static GameState ProduceBuilding(Ruleset ruleset, GameState state, City city, BuildingType building, ImmutableArray<GameEvent>.Builder events)
    {
        var progress = city.ProductionProgress + WorkArea.ProductionPerTurn(ruleset, city.BuildingList);
        if (progress < ruleset.Building(building).Cost)
        {
            return state.WithCity(city with { ProductionProgress = progress });
        }

        events.Add(new BuildingCompletedEvent(city.Id, building));
        return state.WithCity(city with { ProducingBuilding = null, ProductionProgress = 0, Buildings = [.. city.BuildingList, building] });
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

    // A unit rested when its movement is still full and it has no job: it neither moved, attacked nor worked this turn.
    private static GameState Heal(Ruleset ruleset, GameState state, PlayerId actor) => state with
    {
        Units = [.. state.Units.Select(unit => unit.Owner == actor && unit.Damage > 0 && unit.Job is null && unit.MovementLeft >= ruleset.Unit(unit.Type).Movement
            ? unit with { Damage = Math.Max(0, unit.Damage - HealAmount(ruleset, state, unit)) }
            : unit)],
    };

    private static int HealAmount(Ruleset ruleset, GameState state, Unit unit) =>
        state.CityAt(unit.Position)?.Owner == unit.Owner ? ruleset.Combat.Healing.InOwnCity : ruleset.Combat.Healing.PerTurn;

    private static GameState ResetMovement(Ruleset ruleset, GameState state, PlayerId player) => state with
    {
        Units = [.. state.Units.Select(unit => unit.Owner == player ? unit with { MovementLeft = ruleset.Unit(unit.Type).Movement } : unit)],
    };
}
