using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.UnitTests.Core.Builders;

/// <summary>
/// Builds hand-placed game scenarios on a 12×10 plains map. Positions are odd-r offset (column, row); tiles on the same row
/// are exactly |Δcolumn| steps apart. Exploration is computed on <see cref="Build"/>, as in a real game.
/// </summary>
internal sealed class GameStateBuilder
{
    public const int Width = 12;
    public const int Height = 10;

    public static readonly PlayerId P1 = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly PlayerId P2 = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private readonly List<Unit> _units = [];
    private readonly List<City> _cities = [];
    private readonly Dictionary<HexCoord, TerrainType> _terrain = [];
    private readonly Dictionary<HexEdge, RiverType> _rivers = [];
    private readonly HashSet<HexEdge> _bridges = [];
    private readonly Dictionary<HexCoord, ImprovementType> _improvements = [];
    private readonly Dictionary<HexCoord, BuildingType> _buildings = [];
    private readonly Dictionary<HexCoord, CityId?> _territory = [];
    private int _nextId = 1;
    private PlayerId _currentPlayer = P1;
    private int _turn = 1;
    private readonly HashSet<PlayerId> _exploredAll = [];

    public static HexCoord At(int column, int row) => HexCoord.FromOffset(column, row);

    public UnitId AddUnit(
        PlayerId owner,
        UnitType type,
        int column,
        int row,
        int? movementLeft = null,
        int damage = 0,
        UnitJob? job = null,
        (int Column, int Row)? destination = null)
    {
        var id = new UnitId(_nextId++);
        var target = destination is var (targetColumn, targetRow) ? At(targetColumn, targetRow) : (HexCoord?)null;
        _units.Add(new Unit(id, owner, type, At(column, row), movementLeft ?? TestRulesets.Default.Unit(type).Movement, damage, job, target));
        return id;
    }

    public CityId AddCity(
        PlayerId owner,
        int column,
        int row,
        UnitType? producing = null,
        int progress = 0,
        BuildingType? producingBuilding = null,
        ImmutableArray<ResourceAmount>? stock = null,
        int tilesGained = 0,
        bool paid = false,
        (int Column, int Row)? buildingSite = null,
        params BuildingType[] buildings)
    {
        var id = new CityId(_nextId++);
        _cities.Add(new City(
            id,
            owner,
            $"City {id.Value}",
            At(column, row),
            producing ?? TestRulesets.Soldier,
            progress,
            producingBuilding,
            buildings.Length == 0 ? null : [.. buildings],
            stock,
            tilesGained,
            paid,
            ProducingBuildingSite: buildingSite is var (siteColumn, siteRow) ? At(siteColumn, siteRow) : null));
        return id;
    }

    // Changes a city added before, e.g. its upkeep priority or its buildings' damage.
    public GameStateBuilder WithCity(CityId id, Func<City, City> change)
    {
        var index = _cities.FindIndex(city => city.Id == id);
        _cities[index] = change(_cities[index]);
        return this;
    }

    // Gives tiles to a city's territory (null: to no city), on top of the tiles every city claims around itself when it is built
    // (territory.initialRadius, in the order the cities were added), e.g. WithTerritory(city, (5, 7)).
    public GameStateBuilder WithTerritory(CityId? city, params (int Column, int Row)[] tiles)
    {
        foreach (var (column, row) in tiles)
        {
            _territory[At(column, row)] = city;
        }

        return this;
    }

    // Puts an improvement on tiles, e.g. WithImprovement(TestRulesets.Farm, (5, 4)).
    public GameStateBuilder WithImprovement(ImprovementType improvement, params (int Column, int Row)[] tiles)
    {
        foreach (var (column, row) in tiles)
        {
            _improvements[At(column, row)] = improvement;
        }

        return this;
    }

    // Puts a finished building on a tile, e.g. WithBuilding(TestRulesets.Campus, (5, 4)); the city's list is AddCity's buildings.
    public GameStateBuilder WithBuilding(BuildingType building, (int Column, int Row) tile)
    {
        _buildings[At(tile.Column, tile.Row)] = building;
        return this;
    }

    // Puts a bridge on the edges between neighbouring tiles (normally over a river added with WithRiver).
    public GameStateBuilder WithBridge(params ((int Column, int Row) A, (int Column, int Row) B)[] edges)
    {
        foreach (var (a, b) in edges)
        {
            _bridges.Add(HexEdge.Between(At(a.Column, a.Row), At(b.Column, b.Row)) ?? throw new ArgumentException($"{a} and {b} are not neighbours."));
        }

        return this;
    }

    // One city per player in opposite corners keeps both players alive and far from the action.
    public GameStateBuilder WithCapitals()
    {
        AddCity(P1, 0, 0);
        AddCity(P2, Width - 1, Height - 1);
        return this;
    }

    // Puts a terrain other than plains on tiles, e.g. WithTerrain(TestRulesets.Forest, (6, 5), (6, 4)).
    public GameStateBuilder WithTerrain(TerrainType terrain, params (int Column, int Row)[] tiles)
    {
        foreach (var (column, row) in tiles)
        {
            _terrain[At(column, row)] = terrain;
        }

        return this;
    }

    // Puts a river on the edges between neighbouring tiles, e.g. WithRiver(TestRulesets.River, ((5, 5), (6, 5))).
    public GameStateBuilder WithRiver(RiverType river, params ((int Column, int Row) A, (int Column, int Row) B)[] edges)
    {
        foreach (var (a, b) in edges)
        {
            _rivers[HexEdge.Between(At(a.Column, a.Row), At(b.Column, b.Row)) ?? throw new ArgumentException($"{a} and {b} are not neighbours.")] = river;
        }

        return this;
    }

    // Lets a player know the whole map's terrain and rivers, as if they had explored it all.
    public GameStateBuilder WithMapExplored(PlayerId player)
    {
        _exploredAll.Add(player);
        return this;
    }

    public GameStateBuilder WithCurrentPlayer(PlayerId player, int turn = 1)
    {
        _currentPlayer = player;
        _turn = turn;
        return this;
    }

    public GameState Build() => VisibilityCalculator.UpdateExplored(TestRulesets.Default, new GameState
    {
        Version = 0,
        Turn = _turn,
        CurrentPlayer = _currentPlayer,
        Status = GameStatus.InProgress,
        Map = BuildMap(),
        Players = [new Player(P1, PlayerStatus.Active, ExploredBy(P1)), new Player(P2, PlayerStatus.Active, ExploredBy(P2))],
        Units = [.. _units],
        Cities = [.. _cities],
        RngState = 42,
    });

    private ImmutableArray<HexCoord> ExploredBy(PlayerId player) =>
        _exploredAll.Contains(player) ? [.. Enumerable.Range(0, Width * Height).Select(static index => At(index % Width, index / Width))] : [];

    private GameMap BuildMap()
    {
        var map = GameMap.Create(Width, Height, TestRulesets.Plains);
        return map with { Tiles = [.. map.Tiles.Select(tile => WithTerritory(WithRivers(_terrain.TryGetValue(tile.Coord, out var terrain) ? tile with { Terrain = terrain } : tile)))] };
    }

    // The first city (in the order added) within territory.initialRadius claims the tile, unless WithTerritory says otherwise.
    private Tile WithTerritory(Tile tile)
    {
        if (_territory.TryGetValue(tile.Coord, out var owner))
        {
            return tile with { Territory = owner };
        }

        var radius = TestRulesets.Default.Territory.InitialRadius;
        return _cities.FirstOrDefault(city => city.Position.DistanceTo(tile.Coord) <= radius) is { } claimer
            ? tile with { Territory = claimer.Id }
            : tile;
    }

    private Tile WithRivers(Tile tile)
    {
        foreach (var (edge, river) in _rivers.Where(river => river.Key.Owner == tile.Coord))
        {
            tile = tile with { Rivers = (tile.Rivers ?? new TileRivers()).With(edge.Side, river) };
        }

        foreach (var edge in _bridges.Where(edge => edge.Owner == tile.Coord))
        {
            tile = tile with { Bridges = (tile.Bridges ?? new TileBridges()).With(edge.Side) };
        }

        tile = _buildings.TryGetValue(tile.Coord, out var building) ? tile with { Building = building } : tile;
        return _improvements.TryGetValue(tile.Coord, out var improvement) ? tile with { Improvement = improvement } : tile;
    }
}
