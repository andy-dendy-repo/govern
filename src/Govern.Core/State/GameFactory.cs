using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.State;

/// <summary>
/// Creates the initial <see cref="GameState"/>: a plains map, and for each player a settler with a soldier next to it.
/// The first player spawns in the left quarter of the map, the second in the right quarter; the seed picks the exact tiles.
/// </summary>
public static class GameFactory
{
    /// <summary>Creates a new game. The same setup always produces the same state.</summary>
    /// <param name="setup">Players, seed and map size.</param>
    /// <returns>Turn 1, first player to move, version 0.</returns>
    /// <exception cref="ArgumentException">The setup does not have exactly two distinct, non-empty players.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The map size is out of range.</exception>
    public static GameState Create(GameSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ValidatePlayers(setup.Players);

        var map = GameMap.Create(setup.Width, setup.Height);
        var random = new DeterministicRandom(setup.Seed);
        var ids = new HashSet<int>();
        var units = ImmutableArray.CreateBuilder<Unit>();

        for (var index = 0; index < setup.Players.Length; index++)
        {
            var (settlerTile, soldierTile) = SpawnTiles(map, random, isLeftSide: index == 0);
            units.Add(NewUnit(random, ids, setup.Players[index], UnitType.Settler, settlerTile));
            units.Add(NewUnit(random, ids, setup.Players[index], UnitType.Soldier, soldierTile));
        }

        var state = new GameState
        {
            Version = 0,
            Turn = 1,
            CurrentPlayer = setup.Players[0],
            Status = GameStatus.InProgress,
            Map = map,
            Players = [.. setup.Players.Select(static id => new Player(id, PlayerStatus.Active, []))],
            Units = units.ToImmutable(),
            Cities = ImmutableArray<City>.Empty,
            RngState = random.State,
        };

        return VisibilityCalculator.UpdateExplored(state);
    }

    private static void ValidatePlayers(ImmutableArray<PlayerId> players)
    {
        if (players.IsDefault || players.Length != GameSetup.PlayerCount)
        {
            throw new ArgumentException($"A game needs exactly {GameSetup.PlayerCount} players.", nameof(players));
        }

        if (players.Any(static player => player.Value == Guid.Empty) || players.Distinct().Count() != players.Length)
        {
            throw new ArgumentException("Players must be distinct and non-empty.", nameof(players));
        }
    }

    // Ids come from the game's generator, like every later id (GameState.AllocateId), so they reveal no creation count.
    private static Unit NewUnit(DeterministicRandom random, HashSet<int> ids, PlayerId owner, UnitType type, HexCoord tile)
    {
        var id = GameState.NewId(random, ids.Contains);
        ids.Add(id);
        return new Unit(new UnitId(id), owner, type, tile, UnitCatalog.Get(type).Movement);
    }

    // The settler stands in the player's quarter; the soldier stands next to it, towards the map centre.
    private static (HexCoord Settler, HexCoord Soldier) SpawnTiles(GameMap map, DeterministicRandom random, bool isLeftSide)
    {
        var quarter = map.Width / 4;
        var column = isLeftSide ? random.Next(1, quarter) : random.Next(map.Width - quarter, map.Width - 1);
        var row = random.Next(1, map.Height - 1);
        var soldierColumn = isLeftSide ? column + 1 : column - 1;

        return (HexCoord.FromOffset(column, row), HexCoord.FromOffset(soldierColumn, row));
    }
}
