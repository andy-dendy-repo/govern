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
/// Ends the actor's turn: their units' jobs advance (see <see cref="ConstructionRule"/>), their cities add their income to their
/// stock, pay upkeep (see <see cref="UpkeepRule"/>: unsupplied units and buildings lose health, supplied units that rested heal),
/// grow their territory (see <see cref="TerritoryRule"/>) and produce (except a city whose production a job used; an item begins
/// once its resource cost is paid), then the next active player moves with full movement.
/// </summary>
internal static class EndTurnRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor)
    {
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var resting = UpkeepRule.RestingUnits(ruleset, state, actor);
        var (built, usedCities) = ConstructionRule.AdvanceJobs(ruleset, state, actor, events);
        var kept = UpkeepRule.Apply(ruleset, ConstructionRule.CollectYields(ruleset, built, actor), actor, resting, events);
        var grown = TerritoryRule.Grow(ruleset, kept, actor, events);
        var produced = grown.Cities
            .Where(city => city.Owner == actor && !usedCities.Contains(city.Id))
            .Select(static city => city.Id)
            .Aggregate(grown, (current, cityId) => Produce(ruleset, current, current.FindCity(cityId)!, events));

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
        // An item begins when the city pays its whole resource cost; until then it waits and gains nothing.
        if (!city.ProductionPaid)
        {
            var resourceCost = CityProduction.ResourceCost(ruleset, city.ProducingBuilding, city.Producing);
            if (!UpkeepCalculator.Missing(ruleset, city.StockList, resourceCost).IsEmpty)
            {
                return state;
            }

            var stock = UpkeepCalculator.Subtract(ruleset, city.StockList, resourceCost);
            city = city with { Stock = stock.IsEmpty ? null : stock, ProductionPaid = true };
        }

        var perTurn = CityProduction.PerTurn(ruleset, city.WorkingBuildings(ruleset.Upkeep.BuildingHealth));
        if (city.ProducingBuilding is not null)
        {
            return ProduceBuilding(ruleset, state, city, perTurn, events);
        }

        var cost = ruleset.Unit(city.Producing).ProductionCost;
        var progress = Math.Min(city.ProductionProgress + perTurn, cost);
        if (progress < cost || SpawnTile(ruleset, state, city) is not { } tile)
        {
            return state.WithCity(city with { ProductionProgress = progress });
        }

        var (allocated, id) = state.AllocateId();
        var unit = new Unit(new UnitId(id), city.Owner, city.Producing, tile, MovementLeft: 0);
        events.Add(new UnitCreatedEvent(unit.Id, unit.Owner, unit.Type, unit.Position, city.Id));

        var reset = allocated.WithCity(city with { ProductionProgress = 0, ProductionPaid = false });
        return reset with { Units = reset.Units.Add(unit) };
    }

    // A finished building joins the city and stands on its reserved tile, and the city goes back to producing its unit from
    // scratch. The site is still valid: the reservation keeps improvements off it, and its territory changes hands only with the
    // city, whose capture drops the reservation. A game saved before buildings had sites has none; that building stays off the map.
    private static GameState ProduceBuilding(Ruleset ruleset, GameState state, City city, int perTurn, ImmutableArray<GameEvent>.Builder events)
    {
        var building = city.ProducingBuilding!.Value;
        var progress = city.ProductionProgress + perTurn;
        if (progress < ruleset.Building(building).Cost)
        {
            return state.WithCity(city with { ProductionProgress = progress });
        }

        var site = city.ProducingBuildingSite;
        events.Add(new BuildingCompletedEvent(city.Id, building, site ?? city.Position));
        var done = state.WithCity(city with
        {
            ProducingBuilding = null,
            ProducingBuildingSite = null,
            ProductionProgress = 0,
            ProductionPaid = false,
            Buildings = [.. city.BuildingList, building],
        });
        return site is { } coord ? done with { Map = done.Map.WithTile(done.Map.TileAt(coord) with { Building = building }) } : done;
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
