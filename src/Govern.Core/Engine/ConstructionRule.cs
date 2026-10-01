using System.Collections.Immutable;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Engine;

// The end-of-turn economy of one player (EndTurnRule): their units' jobs advance, then each of their cities adds the yields of
// its territory and buildings to its own stock.
internal static class ConstructionRule
{
    // Advances every job of the player's units, oldest first (then by unit id). A job gains its unit's build production, plus
    // the production of the player's city whose territory the job is on, which that city then doesn't spend
    // on its own production this turn; each city feeds one job. A job whose site no longer allows it is dropped. Returns the
    // state and the cities whose production was used.
    public static (GameState State, IReadOnlySet<CityId> UsedCities) AdvanceJobs(
        Ruleset ruleset,
        GameState state,
        PlayerId player,
        ImmutableArray<GameEvent>.Builder events)
    {
        var used = new HashSet<CityId>();
        var working = state.Units
            .Where(unit => unit.Owner == player && unit.Job is not null)
            .OrderBy(static unit => unit.Job!.StartedTurn)
            .ThenBy(static unit => unit.Id.Value)
            .Select(static unit => unit.Id)
            .ToList();

        foreach (var unitId in working)
        {
            var unit = state.FindUnit(unitId)!;
            var job = unit.Job!;
            if (CostOf(ruleset, state, unit, job) is not { } cost)
            {
                state = state.WithUnit(unit with { Job = null });
                continue;
            }

            var progress = job.Progress + ruleset.Unit(unit.Type).BuildProduction;
            if (state.Map.TileAt(unit.Position).Territory is { } territory
                && state.FindCity(territory) is { } city
                && city.Owner == player
                && used.Add(city.Id))
            {
                progress += CityProduction.PerTurn(ruleset, city.BuildingList);
            }

            state = progress >= cost
                ? Complete(state, unit, job, events)
                : state.WithUnit(unit with { Job = job with { Progress = progress } });
        }

        return (state, used);
    }

    // Adds each of the player's cities' income (its territory's tiles, its own yields and its buildings) to its stock.
    public static GameState CollectYields(Ruleset ruleset, GameState state, PlayerId player)
    {
        foreach (var city in state.Cities.Where(city => city.Owner == player).ToList())
        {
            var income = Income(ruleset, state.Map, city);
            var stock = YieldCalculator.Sum(ruleset, city.StockList.Concat(income));
            state = state.WithCity(city with { Stock = stock.IsEmpty ? null : stock });
        }

        return state;
    }

    // What a city adds to its stock this turn.
    public static ImmutableArray<ResourceAmount> Income(Ruleset ruleset, GameMap map, City city)
    {
        var tileYields = TerritoryRule.TilesOf(map, city.Id)
            .SelectMany(tile => YieldCalculator.TileYields(ruleset, tile.Terrain, map.RiversAround(tile.Coord), tile.Improvement));
        return YieldCalculator.CityIncome(ruleset, tileYields, city.BuildingList);
    }

    // The job's cost while its site still allows it; null when it doesn't any more (a bridge finished from the other bank, or
    // the city whose territory it was in changed hands).
    private static int? CostOf(Ruleset ruleset, GameState state, Unit unit, UnitJob job)
    {
        if (job.Improvement is { } improvement)
        {
            var definition = ruleset.Improvement(improvement);
            return BuildImprovementRule.CheckPlacement(definition, BuildImprovementRule.SiteOf(ruleset, state, unit)) is null
                ? definition.Cost
                : null;
        }

        return BuildBridgeRule.CheckPlacement(BuildBridgeRule.SiteOf(ruleset, state, unit, job.BridgeTo!.Value)) is null
            ? ruleset.Bridges.Cost
            : null;
    }

    private static GameState Complete(GameState state, Unit unit, UnitJob job, ImmutableArray<GameEvent>.Builder events)
    {
        var idle = state.WithUnit(unit with { Job = null });
        if (job.Improvement is { } improvement)
        {
            events.Add(new ImprovementBuiltEvent(unit.Id, unit.Position, improvement));
            var tile = idle.Map.TileAt(unit.Position);
            return idle with { Map = idle.Map.WithTile(tile with { Improvement = improvement }) };
        }

        var target = job.BridgeTo!.Value;
        events.Add(new BridgeBuiltEvent(unit.Id, unit.Position, target));
        return idle with { Map = idle.Map.WithBridge(HexEdge.Between(unit.Position, target)!.Value) };
    }
}
