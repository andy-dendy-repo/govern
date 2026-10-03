using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.State;

/// <summary>
/// Creates the initial <see cref="GameState"/> from the game's ruleset: a generated map (forests, hills and so on) or the
/// setup's predefined map, and for each player its starting units. On a generated map the first player spawns in a zone at the
/// left edge, the second at the right edge, and the seed picks the exact spawn tile; on a predefined map the seed picks one of
/// its spawn candidates for the first player and, on its own, one of their mirrors for the second. The other starting units
/// stand in a row from the spawn towards the centre. The map's two halves are mirrors of each other, but the spawns are not,
/// so the map barely hints where the opponent started.
/// </summary>
public static class GameFactory
{
    /// <summary>Creates a new game. The same ruleset and setup always produce the same state.</summary>
    /// <param name="ruleset">The ruleset the game is played with.</param>
    /// <param name="setup">Players, seed and map size.</param>
    /// <returns>Turn 1, first player to move, version 0.</returns>
    /// <exception cref="ArgumentException">
    /// The setup does not have as many distinct, non-empty players as <see cref="SetupRules.Players"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The map size is outside the ruleset's limits.</exception>
    /// <exception cref="ArgumentException">The predefined map uses a terrain the ruleset doesn't have.</exception>
    public static GameState Create(Ruleset ruleset, GameSetup setup)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(setup);
        ValidatePlayers(ruleset.Setup.Players, setup.Players);

        var blank = setup.Map is { } predefined ? CheckMap(ruleset, predefined.Map) : CreateMap(ruleset.Map, setup);
        var random = new DeterministicRandom(setup.Seed);
        var ids = new HashSet<int>();
        var units = ImmutableArray.CreateBuilder<Unit>();

        for (var index = 0; index < setup.Players.Length; index++)
        {
            var spawn = setup.Map is { } map
                ? PredefinedSpawn(map, random, isLeftSide: index == 0)
                : GeneratedSpawn(ruleset, blank, random, isLeftSide: index == 0);
            var tiles = SpawnRow(ruleset, spawn, isLeftSide: index == 0);
            for (var slot = 0; slot < tiles.Length; slot++)
            {
                var definition = ruleset.Unit(ruleset.Setup.StartingUnits[slot]);
                units.Add(NewUnit(random, ids, setup.Players[index], definition, tiles[slot]));
            }
        }

        var state = new GameState
        {
            Version = 0,
            Turn = 1,
            CurrentPlayer = setup.Players[0],
            Status = GameStatus.InProgress,
            Map = setup.Map is null ? MapGenerator.Generate(ruleset, blank, random, [.. units.Select(static unit => unit.Position)]) : blank,
            Players = [.. setup.Players.Select(static id => new Player(id, PlayerStatus.Active, []))],
            Units = units.ToImmutable(),
            Cities = ImmutableArray<City>.Empty,
            RngState = random.State,
        };

        return VisibilityCalculator.UpdateExplored(ruleset, state);
    }

    private static GameMap CreateMap(MapRules rules, GameSetup setup)
    {
        var width = setup.Width ?? rules.DefaultWidth;
        var height = setup.Height ?? rules.DefaultHeight;
        ArgumentOutOfRangeException.ThrowIfLessThan(width, rules.MinSize, nameof(setup));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, rules.MaxSize, nameof(setup));
        ArgumentOutOfRangeException.ThrowIfLessThan(height, rules.MinSize, nameof(setup));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, rules.MaxSize, nameof(setup));

        return GameMap.Create(width, height, rules.DefaultTerrain);
    }

    // A predefined map was validated against a ruleset; this one must allow its size and know its terrains.
    private static GameMap CheckMap(Ruleset ruleset, GameMap map)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Math.Min(map.Width, map.Height), ruleset.Map.MinSize, nameof(GameSetup.Map));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Math.Max(map.Width, map.Height), ruleset.Map.MaxSize, nameof(GameSetup.Map));
        foreach (var terrain in map.Tiles.Select(static tile => tile.Terrain).Distinct())
        {
            _ = ruleset.Terrain(terrain);
        }

        return map;
    }

    private static void ValidatePlayers(int count, ImmutableArray<PlayerId> players)
    {
        if (players.IsDefault || players.Length != count)
        {
            throw new ArgumentException($"A game needs exactly {count} players.", nameof(players));
        }

        if (players.Any(static player => player.Value == Guid.Empty) || players.Distinct().Count() != players.Length)
        {
            throw new ArgumentException("Players must be distinct and non-empty.", nameof(players));
        }
    }

    // Ids come from the game's generator, like every later id (GameState.AllocateId), so they reveal no creation count.
    private static Unit NewUnit(DeterministicRandom random, HashSet<int> ids, PlayerId owner, UnitDefinition definition, HexCoord tile)
    {
        var id = GameState.NewId(random, ids.Contains);
        ids.Add(id);
        return new Unit(new UnitId(id), owner, definition.Id, tile, definition.Movement);
    }

    // The spawn tile lies in the player's zone at their edge of the map. The validator keeps every starting unit on the map and on
    // its own side.
    private static HexCoord GeneratedSpawn(Ruleset ruleset, GameMap map, DeterministicRandom random, bool isLeftSide)
    {
        var spawn = ruleset.Map.Spawn;
        var zone = spawn.ZoneColumns(map.Width);
        var column = isLeftSide ? random.Next(spawn.EdgeMargin, zone) : random.Next(map.Width - zone, map.Width - spawn.EdgeMargin);
        var row = random.Next(spawn.EdgeMargin, map.Height - spawn.EdgeMargin);
        return HexCoord.FromOffset(column, row);
    }

    // One of the map's candidates, or the mirror of one for the second player; the map's validator keeps the row of starting units
    // passable and in the player's half. The pick is visible to its player, so it is a hashed draw.
    private static HexCoord PredefinedSpawn(PredefinedMap map, DeterministicRandom random, bool isLeftSide)
    {
        var spawn = map.Spawns[random.NextHashed(0, map.Spawns.Length)];
        return isLeftSide ? spawn : map.Map.MirrorOf(spawn);
    }

    // The k-th starting unit stands k columns from the spawn towards the centre.
    private static ImmutableArray<HexCoord> SpawnRow(Ruleset ruleset, HexCoord spawn, bool isLeftSide)
    {
        var towardsCentre = isLeftSide ? 1 : -1;
        return [.. Enumerable.Range(0, ruleset.Setup.StartingUnits.Length)
            .Select(slot => HexCoord.FromOffset(spawn.Column + (slot * towardsCentre), spawn.Row))];
    }
}
