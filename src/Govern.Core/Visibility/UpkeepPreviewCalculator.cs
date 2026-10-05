using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

// The view-based upkeep previews behind GameEngine.CityForecast and UnitSupply. They use the same calculators as the end of turn
// (YieldCalculator, UpkeepCalculator) on what the view shows: the viewer's own territory is always visible, and a unit sees every
// tile that supplies it (the validator keeps sight above the supply radius).
internal static class UpkeepPreviewCalculator
{
    public static CityForecast? CityForecast(Ruleset ruleset, PlayerView view, CityId cityId)
    {
        if (view.Cities.FirstOrDefault(city => city.Id == cityId && city.Owner == view.Viewer) is not { Producing: { } producing } city)
        {
            return null;
        }

        var income = ViewPreviewCalculator.CityIncome(ruleset, view, cityId);
        // Science goes to the player's budget, not the stock (as in ConstructionRule.CollectYields).
        var stock = YieldCalculator.Sum(ruleset, city.StockList.Concat(income.Where(amount => amount.Resource != ruleset.Science.Resource)));
        var upkeep = UpkeepCalculator.Pay(ruleset, stock, city.BuildingList, UnitsOn(ruleset, view, cityId), city.UpkeepPriority);
        var missing = city.ProductionPaid
            ? []
            : UpkeepCalculator.Missing(ruleset, upkeep.Stock, CityProduction.ResourceCost(ruleset, city.ProducingBuilding, producing));
        var damage = UpkeepCalculator.DamageAfter(ruleset, city.BuildingList, city.DamageOf, upkeep.UnpaidBuildings);
        var working = city.BuildingList.Where(building => !damage.Any(entry => entry.Building == building && entry.Damage >= ruleset.Upkeep.BuildingHealth));
        var sources = CityProduction.Sources(ruleset, working);
        var perTurn = missing.IsEmpty ? sources.Sum(static source => source.Amount) : 0;
        var caps = CityStorage.Caps(ruleset, working);
        var kept = city.ProductionPaid || !missing.IsEmpty
            ? upkeep.Stock
            : UpkeepCalculator.Subtract(ruleset, upkeep.Stock, CityProduction.ResourceCost(ruleset, city.ProducingBuilding, producing));
        return new CityForecast(income, upkeep, missing, perTurn, caps, CityStorage.Excess(ruleset, AfterGrowth(ruleset, city, kept), caps), missing.IsEmpty ? sources : []);
    }

    // The stock once the city has bought its next tile when it can pay for it (TerritoryRule.Grow). Whether a tile is left to
    // claim isn't checked: the preview assumes one is, so a city with none may lose a little more culture than it shows.
    private static ImmutableArray<ResourceAmount> AfterGrowth(Ruleset ruleset, CityView city, ImmutableArray<ResourceAmount> stock)
    {
        var price = ruleset.Territory.NextTileCost(city.TilesGained);
        return YieldCalculator.AmountOf(stock, ruleset.Territory.GrowthResource) >= price
            ? YieldCalculator.Sum(ruleset, stock.Append(new ResourceAmount(ruleset.Territory.GrowthResource, -price)))
            : stock;
    }

    public static UnitSupply? UnitSupply(Ruleset ruleset, PlayerView view, UnitId unitId)
    {
        if (view.FindOwnUnit(unitId) is not { } unit)
        {
            return null;
        }

        var upkeep = ruleset.Unit(unit.Type).Upkeep;
        if (view.TileAt(unit.Position).TerritoryCity is { } cityId)
        {
            var paid = CityForecast(ruleset, view, cityId) is { } forecast && !forecast.Upkeep.UnpaidUnits.Contains(unitId);
            return new UnitSupply(cityId, paid ? [] : YieldCalculator.Sum(ruleset, upkeep));
        }

        return new UnitSupply(null, UpkeepCalculator.Missing(ruleset, LandYields(ruleset, view, unit.Position), upkeep));
    }

    private static IEnumerable<UpkeepUnit> UnitsOn(Ruleset ruleset, PlayerView view, CityId cityId) => view.Units
        .Where(unit => unit.Owner == view.Viewer && view.TileAt(unit.Position).TerritoryCity == cityId)
        .Select(unit => new UpkeepUnit(unit.Id, unit.Type, ruleset.Combat.MaxHealth - unit.Health));

    private static IEnumerable<ResourceAmount> LandYields(Ruleset ruleset, PlayerView view, HexCoord position) => position
        .WithinRadius(ruleset.Upkeep.SupplyRadius)
        .Where(view.Contains)
        .Select(coord => (Coord: coord, Tile: view.TileAt(coord)))
        .Where(static entry => entry.Tile.Terrain is not null)
        .SelectMany(entry => YieldCalculator.TileYields(ruleset, entry.Tile.Terrain!.Value, view.RiversAround(entry.Coord), entry.Tile.Improvement));
}
