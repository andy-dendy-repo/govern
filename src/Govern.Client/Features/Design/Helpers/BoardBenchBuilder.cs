using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Design.Helpers;

/// <summary>
/// The views the 3D board bench (<c>/design/board</c>) draws, built here as views (the client never generates games): a map as
/// large as the ruleset allows, with patches of every terrain, rivers, cities with their territory, improvements and buildings,
/// and an army on every few tiles. Either with every tile in sight (the worst case for the board) or as an opening would look,
/// with a few units in sight of a small corner. The same ruleset always gives the same views.
/// </summary>
public static class BoardBenchBuilder
{
    /// <summary>The bench's own player, who sees the view.</summary>
    public static readonly PlayerId You = new(Guid.Parse("0199b3c4-0000-7000-8000-00000000000a"));

    /// <summary>The other player.</summary>
    public static readonly PlayerId Them = new(Guid.Parse("0199b3c4-0000-7000-8000-00000000000b"));

    private const int Sight = 3;
    private const int Remembered = 6;

    /// <summary>Builds the bench's view.</summary>
    /// <param name="ruleset">The ruleset (its largest map size and its terrains, rivers, units and buildings are used).</param>
    /// <param name="revealed">Every tile in sight with the whole army; otherwise an opening, with three units in the west.</param>
    /// <returns>The view, at version 1.</returns>
    public static PlayerView Build(Ruleset ruleset, bool revealed)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        var size = ruleset.Map.MaxSize;
        var terrains = Enumerable.Range(0, size * size)
            .ToDictionary(index => HexCoord.FromOffset(index % size, index / size), index => TerrainAt(ruleset, index % size, index / size));
        var passable = terrains.Where(pair => ruleset.Terrain(pair.Value).IsPassable).Select(static pair => pair.Key).ToHashSet();
        var cities = Cities(passable, size);
        var units = revealed ? Army(ruleset, terrains, cities, size) : Opening(ruleset, passable, size);
        var visible = revealed ? null : units.SelectMany(static unit => unit.Position.WithinRadius(Sight)).ToHashSet();
        var tiles = terrains.Keys
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)
            .Select(coord => Tile(new BenchMap(ruleset, size, cities, visible, units[0].Position), coord, terrains[coord]));

        return new PlayerView(
            You, 1, 1, You, GameStatus.InProgress, null, size, size, [.. tiles], units,
            revealed ? cities : [],
            [new PlayerSummary(You, PlayerStatus.Active), new PlayerSummary(Them, PlayerStatus.Active)]);
    }

    /// <summary>
    /// The next view: each of the viewer's units steps one tile east when it may stand there (land, or water for a ship) and the tile is free, as a turn of moves
    /// would. Tiles and cities stay as they are.
    /// </summary>
    /// <param name="ruleset">The ruleset.</param>
    /// <param name="view">The current view.</param>
    /// <returns>The view with the units moved and a new version.</returns>
    public static PlayerView March(Ruleset ruleset, PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        var taken = view.Units.Select(static unit => unit.Position).ToHashSet();
        var units = view.Units.Select(unit =>
        {
            var east = HexCoord.FromOffset(unit.Position.Column + 1, unit.Position.Row);
            if (unit.Owner != view.Viewer || !view.Contains(east) || taken.Contains(east)
                || view.TileAt(east).Terrain is not { } terrain || !ruleset.Terrain(terrain).AllowsDomain(ruleset.Unit(unit.Type).Domain))
            {
                return unit;
            }

            taken.Remove(unit.Position);
            taken.Add(east);
            return unit with { Position = east };
        });

        return view with { Units = [.. units], Version = view.Version + 1 };
    }

    // Patches of every terrain, the first (the default) the most common.
    private static TerrainType TerrainAt(Ruleset ruleset, int column, int row)
    {
        var share = Math.Pow(Noise(column / 3, row / 3), 1.6);
        return ruleset.Terrains[(int)(share * ruleset.Terrains.Length)].Id;
    }

    // A city on every tenth tile or so, the viewer's in the west half.
    private static ImmutableArray<CityView> Cities(HashSet<HexCoord> passable, int size) =>
    [
        .. passable
            .Where(static coord => coord.Column % 10 == 5 && coord.Row % 10 == 5)
            .OrderBy(static coord => coord.Row)
            .ThenBy(static coord => coord.Column)
            .Select((coord, index) => new CityView(
                new CityId(index + 1), OwnerAt(coord, size), $"Bench {index + 1}", coord, Producing: null, ProductionProgress: null)),
    ];

    // A unit on every few tiles outside the cities, of every type, a third of them hurt: land units on passable land, ships on
    // water.
    private static ImmutableArray<UnitView> Army(Ruleset ruleset, Dictionary<HexCoord, TerrainType> terrains, ImmutableArray<CityView> cities, int size)
    {
        var occupied = cities.Select(static city => city.Position).ToHashSet();
        var byDomain = ruleset.Units.GroupBy(static unit => unit.Domain).ToDictionary(static group => group.Key, static group => group.Select(static unit => unit.Id).ToList());
        var spots = terrains
            .Where(pair => pair.Key.Column % 4 == 1 && pair.Key.Row % 3 == 1 && !occupied.Contains(pair.Key))
            .Select(pair => (Coord: pair.Key, Types: byDomain.FirstOrDefault(domain => ruleset.Terrain(pair.Value).AllowsDomain(domain.Key)).Value))
            .Where(static spot => spot.Types is not null)
            .OrderBy(static spot => spot.Coord.Row)
            .ThenBy(static spot => spot.Coord.Column)
            .ToList();
        return [.. spots.Select((spot, index) => Unit(index, spot.Types[index % spot.Types.Count], spot.Coord, OwnerAt(spot.Coord, size), index % 3 == 0 ? ruleset.Combat.MaxHealth / 2 : ruleset.Combat.MaxHealth))];
    }

    // Three of the viewer's units on the passable tiles nearest a spot in the west, as at the start of a game.
    private static ImmutableArray<UnitView> Opening(Ruleset ruleset, HashSet<HexCoord> passable, int size)
    {
        var spot = HexCoord.FromOffset(3, size / 2);
        return
        [
            .. passable
                .OrderBy(coord => coord.DistanceTo(spot))
                .ThenBy(static coord => coord.Row)
                .ThenBy(static coord => coord.Column)
                .Take(3)
                .Select((coord, index) => Unit(index, ruleset.Units[index % ruleset.Units.Length].Id, coord, You, ruleset.Combat.MaxHealth)),
        ];
    }

    private static UnitView Unit(int index, UnitType type, HexCoord coord, PlayerId owner, int health) =>
        new(new UnitId(index + 1), owner, type, coord, owner == You ? 1 : null, health);

    // A tile as the viewer sees it: in sight (with rivers, territory, improvement and building), remembered, or unknown.
    private static TileView Tile(BenchMap map, HexCoord coord, TerrainType terrain)
    {
        var ruleset = map.Ruleset;
        var rivers = RiversAt(ruleset, coord, map.Size);
        if (map.Visible is { } visible && !visible.Contains(coord))
        {
            return coord.DistanceTo(map.Start) <= Remembered + Sight
                ? new TileView(TileVisibility.Explored, terrain, rivers)
                : new TileView(TileVisibility.Unexplored, null);
        }

        var city = map.Cities.FirstOrDefault(city => city.Position.DistanceTo(coord) <= 1);
        var ring = city is null ? -1 : Ring(city.Position, coord);
        return new TileView(
            TileVisibility.Visible,
            terrain,
            rivers,
            Improvement: ring is 1 or 3 ? ImprovementOn(ruleset, terrain) : null,
            TerritoryOwner: city?.Owner,
            TerritoryCity: city is { } own && own.Owner == You ? own.Id : null,
            Building: ring == 5 && ruleset.Buildings.Length > 0 ? ruleset.Buildings[0].Id : null);
    }

    // A river along the east side of every eighth column, of each kind in turn, away from the top and bottom rows.
    private static TileRivers? RiversAt(Ruleset ruleset, HexCoord coord, int size) =>
        coord.Column % 8 == 3 && coord.Row > 1 && coord.Row < size - 2 && ruleset.Rivers.Length > 0
            ? new TileRivers(East: ruleset.Rivers[coord.Column / 8 % ruleset.Rivers.Length].Id)
            : null;

    private static ImprovementType? ImprovementOn(Ruleset ruleset, TerrainType terrain) =>
        ruleset.Improvements.FirstOrDefault(improvement => improvement.Terrains.Contains(terrain))?.Id;

    // Which of a city's neighbours a tile is (0 to 5), or -1 for the city's own tile.
    private static int Ring(HexCoord city, HexCoord coord) => city.Neighbors().ToList().IndexOf(coord);

    private static PlayerId OwnerAt(HexCoord coord, int size) => coord.Column < size / 2 ? You : Them;

    // A number in [0, 1) that looks random but depends only on the cell.
    private static double Noise(int x, int y)
    {
        var hash = unchecked((uint)((x * 374761393) + (y * 668265263)));
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177);
        return (hash ^ (hash >> 16)) / ((double)uint.MaxValue + 1);
    }

    // What every tile of a bench view is built from: the cities and, for an opening, the tiles in sight and where the units start.
    private sealed record BenchMap(Ruleset Ruleset, int Size, ImmutableArray<CityView> Cities, HashSet<HexCoord>? Visible, HexCoord Start);
}
