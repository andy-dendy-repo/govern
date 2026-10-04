using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Engine;

// The end-of-turn upkeep of one player (EndTurnRule), after their cities' income: each city pays for its buildings and for the
// player's units on its territory from its stock; a unit elsewhere lives off the land around it. An unpaid building or unit
// loses health (a unit never below 1, a building down to 0, where it gives nothing); a paid building recovers, and a supplied
// unit that rested heals.
internal static class UpkeepRule
{
    // The player's hurt units that neither moved, attacked nor worked this turn; taken before the jobs advance.
    public static IReadOnlySet<UnitId> RestingUnits(Ruleset ruleset, GameState state, PlayerId player) => state.Units
        .Where(unit => unit.Owner == player && unit.Damage > 0 && unit.Job is null && unit.MovementLeft >= ruleset.Unit(unit.Type).Movement)
        .Select(static unit => unit.Id)
        .ToHashSet();

    // Also returns, per city, the upkeep it couldn't pay (of its unpaid buildings and units), which raises its demand (TradeRule).
    public static (GameState State, IReadOnlyDictionary<CityId, ImmutableArray<ResourceAmount>> Shortages) Apply(
        Ruleset ruleset,
        GameState state,
        PlayerId player,
        IReadOnlySet<UnitId> resting,
        ImmutableArray<GameEvent>.Builder events)
    {
        var unsupplied = new HashSet<UnitId>();
        var shortages = new Dictionary<CityId, ImmutableArray<ResourceAmount>>();
        foreach (var city in state.Cities.Where(city => city.Owner == player).OrderBy(static city => city.Id.Value).ToList())
        {
            var result = UpkeepCalculator.Pay(ruleset, city.StockList, city.BuildingList, UnitsOn(state, city), city.UpkeepPriority);
            unsupplied.UnionWith(result.UnpaidUnits);
            shortages[city.Id] = Unpaid(ruleset, state, result);
            var damage = BuildingDamage(ruleset, city, result.UnpaidBuildings, events);
            state = state.WithCity(city with
            {
                Stock = result.Stock.IsEmpty ? null : result.Stock,
                BuildingDamage = damage.IsEmpty ? null : damage,
            });
        }

        foreach (var unit in state.Units.Where(unit => unit.Owner == player && PayingCity(state, unit) is null))
        {
            if (!UpkeepCalculator.Missing(ruleset, LandYields(ruleset, state.Map, unit.Position), ruleset.Unit(unit.Type).Upkeep).IsEmpty)
            {
                unsupplied.Add(unit.Id);
            }
        }

        var kept = state with
        {
            Units = [.. state.Units.Select(unit => unit.Owner != player ? unit
                : unsupplied.Contains(unit.Id) ? Starve(ruleset, unit, events)
                : resting.Contains(unit.Id) ? Heal(ruleset, state, unit)
                : unit)],
        };
        return (kept, shortages);
    }

    // The upkeep of what a city couldn't keep up.
    private static ImmutableArray<ResourceAmount> Unpaid(Ruleset ruleset, GameState state, UpkeepResult result) => YieldCalculator.Sum(
        ruleset,
        result.UnpaidBuildings.SelectMany(building => ruleset.Building(building).Upkeep)
            .Concat(result.UnpaidUnits.SelectMany(id => ruleset.Unit(state.FindUnit(id)!.Type).Upkeep)));

    // The player's units on a city's territory, which the city keeps up.
    public static IEnumerable<UpkeepUnit> UnitsOn(GameState state, City city) => state.Units
        .Where(unit => unit.Owner == city.Owner && state.Map.TileAt(unit.Position).Territory == city.Id)
        .Select(static unit => new UpkeepUnit(unit.Id, unit.Type, unit.Damage));

    // The owner's city whose territory the unit stands on; null on neutral or enemy land.
    public static City? PayingCity(GameState state, Unit unit) =>
        state.Map.TileAt(unit.Position).Territory is { } cityId && state.FindCity(cityId) is { } city && city.Owner == unit.Owner ? city : null;

    // What the tiles within the supply radius give, the unit's own tile included.
    public static IEnumerable<ResourceAmount> LandYields(Ruleset ruleset, GameMap map, HexCoord position) => position
        .WithinRadius(ruleset.Upkeep.SupplyRadius)
        .Where(map.Contains)
        .Select(map.TileAt)
        .SelectMany(tile => YieldCalculator.TileYields(ruleset, tile.Terrain, map.RiversAround(tile.Coord), tile.Improvement));

    // Unpaid buildings lose health down to 0; paid, hurt ones recover.
    private static ImmutableArray<BuildingDamage> BuildingDamage(Ruleset ruleset, City city, ImmutableArray<BuildingType> unpaid, ImmutableArray<GameEvent>.Builder events)
    {
        var damage = UpkeepCalculator.DamageAfter(ruleset, city.BuildingList, city.DamageOf, unpaid);
        foreach (var entry in damage.Where(entry => entry.Damage > city.DamageOf(entry.Building)))
        {
            events.Add(new BuildingDamagedEvent(city.Id, entry.Building, ruleset.Upkeep.BuildingHealth - entry.Damage));
        }

        return damage;
    }

    // An unsupplied unit loses health but keeps at least 1.
    private static Unit Starve(Ruleset ruleset, Unit unit, ImmutableArray<GameEvent>.Builder events)
    {
        var maxHealth = ruleset.Combat.MaxHealth;
        var damage = Math.Max(unit.Damage, Math.Min(unit.Damage + ruleset.Upkeep.ShortageDamage, maxHealth - 1));
        events.Add(new UnitStarvedEvent(unit.Id, maxHealth - damage));
        return unit with { Damage = damage };
    }

    // A supplied unit that rested heals, more in one of its owner's cities, or, for a ship, next to its owner's harbour.
    private static Unit Heal(Ruleset ruleset, GameState state, Unit unit)
    {
        var heal = AtHome(ruleset, state, unit) ? ruleset.Combat.Healing.InOwnCity : ruleset.Combat.Healing.PerTurn;
        return unit with { Damage = Math.Max(0, unit.Damage - heal) };
    }

    // A ship never enters a city, so its home is next to a tile of its owner's territory holding the building it is made in.
    private static bool AtHome(Ruleset ruleset, GameState state, Unit unit)
    {
        var definition = ruleset.Unit(unit.Type);
        if (!definition.IsNaval)
        {
            return state.CityAt(unit.Position)?.Owner == unit.Owner;
        }

        return definition.RequiresBuilding is { } needed && unit.Position.Neighbors()
            .Where(state.Map.Contains)
            .Select(state.Map.TileAt)
            .Any(tile => tile.Building is { } building && ruleset.IsKindOf(building, needed)
                && tile.Territory is { } city && state.FindCity(city)?.Owner == unit.Owner);
    }
}
