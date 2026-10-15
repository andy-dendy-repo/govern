using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Ends the actor's turn: their resting units heal, their units' building jobs advance, their cities yield resources and
/// produce, every city's stock is cut to its storage caps, and the next active player moves (their units with a destination walk
/// on first).
/// </summary>
public sealed record EndTurnCommand : GameCommand;

/// <summary>
/// Ends the actor's turn: their units' jobs advance (see <see cref="ConstructionRule"/>), their cities add their income to their
/// stock, pay upkeep (see <see cref="UpkeepRule"/>: unsupplied units and buildings lose health, supplied units that rested heal),
/// swap the goods of the accepted deals they host and move their demand levels (see <see cref="TradeRule"/>), grow their territory (see <see cref="TerritoryRule"/>) and produce (except a city whose production a job used; an item begins
/// once its resource cost is paid); then every city's stock is cut to its storage caps (see <see cref="CityStorage"/>), and the next active player moves with full movement, and their units with a destination walk
/// on (see <see cref="AutoMoveRule"/>).
/// </summary>
internal static class EndTurnRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor)
    {
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var resting = UpkeepRule.RestingUnits(ruleset, state, actor);
        var (built, usedCities) = ConstructionRule.AdvanceJobs(ruleset, state, actor, events);
        var (kept, shortages) = UpkeepRule.Apply(ruleset, ConstructionRule.CollectYields(ruleset, built, actor), actor, resting, events);
        var traded = TradeRule.UpdateDemand(ruleset, TradeRule.Exchange(ruleset, kept, actor, events), actor, shortages);
        var grown = TerritoryRule.Grow(ruleset, traded, actor, events);
        var produced = grown.Cities
            .Where(city => city.Owner == actor && !usedCities.Contains(city.Id))
            .Select(static city => city.Id)
            .Aggregate(grown, (current, cityId) => Produce(ruleset, current, current.FindCity(cityId)!, events));

        return new GameUpdate(PassTurn(ruleset, Store(ruleset, produced), events), events.ToImmutable());
    }

    // Cuts every city's stock to its storage caps, once this turn's income has paid its upkeep and costs. Every city, not only
    // the actor's: the deals the actor's cities host deliver goods to the other side too.
    private static GameState Store(Ruleset ruleset, GameState state) => state with
    {
        Cities = [.. state.Cities.Select(city =>
        {
            var stock = CityStorage.Cut(ruleset, city.StockList, CityStorage.Caps(ruleset, city.WorkingBuildings(ruleset.Upkeep.BuildingHealth)));
            return stock.SequenceEqual(city.StockList) ? city : city with { Stock = stock.IsEmpty ? null : stock };
        })],
    };

    // Hands the turn to the next active player in turn order; the round number increases when it wraps around. Their units get
    // full movement, and those with a destination walk on at once, so the player sees where they got to before acting. Also
    // used when the current player leaves the game on their turn (VictoryRule).
    internal static GameState PassTurn(Ruleset ruleset, GameState state, ImmutableArray<GameEvent>.Builder events)
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
            var started = ResetMovement(ruleset, state with { CurrentPlayer = next.Id, Turn = turn }, next.Id);
            return AutoMoveRule.WalkAll(ruleset, started, next.Id, events);
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
        // A finished trader waits while its city already has as many traders as it may.
        var traderWaits = ruleset.Unit(city.Producing).CanTrade && TradeRule.TradersOf(ruleset, state, city.Id) >= ruleset.Trade.TradersPerCity;
        if (progress < cost || traderWaits || SpawnTile(ruleset, state, city) is not { } tile)
        {
            return state.WithCity(city with { ProductionProgress = progress });
        }

        var (allocated, id) = state.AllocateId();
        var unit = new Unit(new UnitId(id), city.Owner, city.Producing, tile, MovementLeft: 0, HomeCity: ruleset.Unit(city.Producing).CanTrade ? city.Id : null);
        events.Add(new UnitCreatedEvent(unit.Id, unit.Owner, unit.Type, unit.Position, city.Id));

        // The city goes on with the newest version of the unit its owner may produce.
        var next = ScienceCalculator.NextProduction(ruleset, state.FindPlayer(city.Owner)!.NodeList, city.Producing);
        var reset = allocated.WithCity(city with { Producing = next, ProductionProgress = 0, ProductionPaid = false });
        return reset with { Units = reset.Units.Add(unit) };
    }

    // A finished building joins the city and stands on its reserved tile, and the city goes back to producing its unit from
    // scratch (its newest version the owner may produce, like after a finished unit). The site is still valid: the reservation keeps improvements off it, and its territory changes hands only with the
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
            Producing = ScienceCalculator.NextProduction(ruleset, state.FindPlayer(city.Owner)!.NodeList, city.Producing),
            ProducingBuilding = null,
            ProducingBuildingSite = null,
            ProductionProgress = 0,
            ProductionPaid = false,
            Buildings = [.. city.BuildingList, building],
        });
        return site is { } coord ? done with { Map = done.Map.WithTile(done.Map.TileAt(coord) with { Building = building }) } : done;
    }

    // The city tile when free, otherwise the first free neighbour a unit could stand on; null when surrounded (a trader shares
    // tiles, so it always appears on the city tile). A ship is
    // launched instead on the first free water tile next to its building (the harbour, or an upgrade of it) in the city's territory.
    private static HexCoord? SpawnTile(Ruleset ruleset, GameState state, City city)
    {
        var definition = ruleset.Unit(city.Producing);
        if (definition.IsNaval)
        {
            return LaunchTile(ruleset, state, city, definition);
        }

        foreach (var coord in city.Position.Neighbors().Prepend(city.Position))
        {
            var ownCityOrNone = state.CityAt(coord) is not { } other || other.Owner == city.Owner;
            if (state.Map.IsPassable(ruleset, coord) && (definition.SharesTiles || state.HolderAt(ruleset, coord) is null) && ownCityOrNone)
            {
                return coord;
            }
        }

        return null;
    }

    // Row-major over the map, then the neighbours in their fixed order, so the tile never depends on anything but the state.
    private static HexCoord? LaunchTile(Ruleset ruleset, GameState state, City city, UnitDefinition ship)
    {
        var docks = state.Map.Tiles.Where(tile => tile.Territory == city.Id
            && tile.Building is { } building && ship.RequiresBuilding is { } needed && ruleset.IsKindOf(building, needed));
        foreach (var dock in docks)
        {
            foreach (var coord in dock.Coord.Neighbors())
            {
                if (state.Map.Contains(coord) && ruleset.Terrain(state.Map.TileAt(coord).Terrain).AllowsDomain(ship.Domain) && state.HolderAt(ruleset, coord) is null)
                {
                    return coord;
                }
            }
        }

        return null;
    }

    private static GameState ResetMovement(Ruleset ruleset, GameState state, PlayerId player) => state with
    {
        Units = [.. state.Units.Select(unit => unit.Owner == player ? unit with { MovementLeft = ruleset.Unit(unit.Type).Movement } : unit)],
    };
}
