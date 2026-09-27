using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Hexes;
using Govern.Core.Maps;
using Govern.Core.Players;
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
    private int _nextId = 1;
    private PlayerId _currentPlayer = P1;
    private int _turn = 1;

    public static HexCoord At(int column, int row) => HexCoord.FromOffset(column, row);

    public UnitId AddUnit(PlayerId owner, UnitType type, int column, int row, int? movementLeft = null)
    {
        var id = new UnitId(_nextId++);
        _units.Add(new Unit(id, owner, type, At(column, row), movementLeft ?? UnitCatalog.Get(type).Movement));
        return id;
    }

    public CityId AddCity(PlayerId owner, int column, int row, UnitType producing = UnitType.Soldier, int progress = 0)
    {
        var id = new CityId(_nextId++);
        _cities.Add(new City(id, owner, $"City {id.Value}", At(column, row), producing, progress));
        return id;
    }

    // One city per player in opposite corners keeps both players alive and far from the action.
    public GameStateBuilder WithCapitals()
    {
        AddCity(P1, 0, 0);
        AddCity(P2, Width - 1, Height - 1);
        return this;
    }

    public GameStateBuilder WithCurrentPlayer(PlayerId player, int turn = 1)
    {
        _currentPlayer = player;
        _turn = turn;
        return this;
    }

    public GameState Build() => VisibilityCalculator.UpdateExplored(new GameState
    {
        Version = 0,
        Turn = _turn,
        CurrentPlayer = _currentPlayer,
        Status = GameStatus.InProgress,
        Map = GameMap.Create(Width, Height),
        Players = [new Player(P1, PlayerStatus.Active, []), new Player(P2, PlayerStatus.Active, [])],
        Units = [.. _units],
        Cities = [.. _cities],
        NextId = _nextId,
        RngState = 42,
    });
}
