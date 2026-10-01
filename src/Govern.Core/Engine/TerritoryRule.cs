using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Engine;

// City territory: the tiles a city owns (Tile.Territory). A new city claims the unowned tiles around it; at the end of its
// owner's turn a city that can pay for the next tile claims it. No randomness: the next tile is always the same for the same map.
internal static class TerritoryRule
{
    // A new city's first claim: its own tile and every unowned tile within territory.initialRadius.
    public static GameMap ClaimAround(Ruleset ruleset, GameMap map, City city)
    {
        foreach (var coord in city.Position.WithinRadius(ruleset.Territory.InitialRadius).Where(map.Contains))
        {
            if (map.TileAt(coord) is { Territory: null } tile)
            {
                map = map.WithTile(tile with { Territory = city.Id });
            }
        }

        return map;
    }

    // Each of the player's cities (by id) that holds the price of its next tile pays it and claims the tile. A city with no tile
    // left to claim keeps its resources.
    public static GameState Grow(Ruleset ruleset, GameState state, PlayerId player, ImmutableArray<GameEvent>.Builder events)
    {
        var growing = state.Cities.Where(city => city.Owner == player).OrderBy(static city => city.Id.Value).Select(static city => city.Id).ToList();
        foreach (var cityId in growing)
        {
            var city = state.FindCity(cityId)!;
            var price = ruleset.Territory.NextTileCost(city.TilesGained);
            var resource = ruleset.Territory.GrowthResource;
            if (YieldCalculator.AmountOf(city.StockList, resource) < price || NextTile(ruleset, state.Map, city) is not { } coord)
            {
                continue;
            }

            var stock = YieldCalculator.Sum(ruleset, city.StockList.Append(new ResourceAmount(resource, -price)));
            var tile = state.Map.TileAt(coord);
            state = state.WithCity(city with { Stock = stock.IsEmpty ? null : stock, TilesGained = city.TilesGained + 1 });
            state = state with { Map = state.Map.WithTile(tile with { Territory = city.Id }) };
            events.Add(new TerritoryGrewEvent(city.Id, city.Owner, coord));
        }

        return state;
    }

    // The tile a city claims next: an unowned tile next to its territory and within territory.maxRadius of it, with the highest
    // total yield, then the nearest to the city, then the first by row and column. Null when there is none.
    public static HexCoord? NextTile(Ruleset ruleset, GameMap map, City city)
    {
        var owned = map.Tiles.Where(tile => tile.Territory == city.Id).Select(static tile => tile.Coord).ToHashSet();
        return owned
            .SelectMany(static coord => coord.Neighbors())
            .Distinct()
            .Where(coord => map.Contains(coord) && map.TileAt(coord).Territory is null && city.Position.DistanceTo(coord) <= ruleset.Territory.MaxRadius)
            .Select(coord => (Coord: coord, Total: YieldCalculator.TileYields(ruleset, map.TileAt(coord).Terrain, map.RiversAround(coord), improvement: null).Sum(static amount => amount.Amount)))
            .OrderByDescending(static candidate => candidate.Total)
            .ThenBy(candidate => city.Position.DistanceTo(candidate.Coord))
            .ThenBy(static candidate => candidate.Coord.Row)
            .ThenBy(static candidate => candidate.Coord.Column)
            .Select(static candidate => (HexCoord?)candidate.Coord)
            .FirstOrDefault();
    }

    // Every tile a city owns.
    public static IEnumerable<Tile> TilesOf(GameMap map, CityId city) => map.Tiles.Where(tile => tile.Territory == city);
}
