using System.Collections.Immutable;
using Govern.Core.Cities;
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
    private int _nextId = 1;
    private PlayerId _currentPlayer = P1;
    private int _turn = 1;

    public static HexCoord At(int column, int row) => HexCoord.FromOffset(column, row);

    public UnitId AddUnit(PlayerId owner, UnitType type, int column, int row, int? movementLeft = null, int damage = 0)
    {
        var id = new UnitId(_nextId++);
        _units.Add(new Unit(id, owner, type, At(column, row), movementLeft ?? TestRulesets.Default.Unit(type).Movement, damage));
        return id;
    }

    public CityId AddCity(PlayerId owner, int column, int row, UnitType? producing = null, int progress = 0)
    {
        var id = new CityId(_nextId++);
        _cities.Add(new City(id, owner, $"City {id.Value}", At(column, row), producing ?? TestRulesets.Soldier, progress));
        return id;
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
        Players = [new Player(P1, PlayerStatus.Active, []), new Player(P2, PlayerStatus.Active, [])],
        Units = [.. _units],
        Cities = [.. _cities],
        RngState = 42,
    });

    private GameMap BuildMap()
    {
        var map = GameMap.Create(Width, Height, TestRulesets.Plains);
        return map with { Tiles = [.. map.Tiles.Select(tile => WithRivers(_terrain.TryGetValue(tile.Coord, out var terrain) ? tile with { Terrain = terrain } : tile))] };
    }

    private Tile WithRivers(Tile tile)
    {
        foreach (var (edge, river) in _rivers.Where(river => river.Key.Owner == tile.Coord))
        {
            tile = tile with { Rivers = (tile.Rivers ?? new TileRivers()).With(edge.Side, river) };
        }

        return tile;
    }
}
