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
/// setup's predefined map, and for each player its starting units. On a generated map for two players the first spawns in a
/// zone at the left edge, the second at the right edge, and the seed picks the exact spawn tile before the terrain is laid; with
/// three or four players each spawns in a zone of their own (<see cref="SpawnRules"/>), drawn after the terrain among the zone's
/// fair starts. On a predefined map the seed picks one candidate of each player's spawn group on its own. The other starting
/// units stand in a row from the spawn towards the centre. A generated map's halves are mirrors of each other, but the spawns
/// are not, so the map barely hints where the others started.
/// </summary>
public static class GameFactory
{
    /// <summary>Creates a new game. The same ruleset and setup always produce the same state.</summary>
    /// <param name="ruleset">The ruleset the game is played with.</param>
    /// <param name="setup">Players, seed and map size.</param>
    /// <returns>Turn 1, first player to move, version 0.</returns>
    /// <exception cref="ArgumentException">
    /// The setup does not have distinct, non-empty players, as many as <see cref="SetupRules"/> allows, or a predefined map is not
    /// made for that many players, or the turn mode is unknown.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The map size is outside the ruleset's limits.</exception>
    /// <exception cref="ArgumentException">The predefined map uses a terrain the ruleset doesn't have.</exception>
    public static GameState Create(Ruleset ruleset, GameSetup setup)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(setup);
        ValidateSetup(ruleset.Setup, setup);

        var count = setup.Players.Length;
        var map = setup.Map is { } predefined ? CheckMap(ruleset, predefined.Map) : CreateMap(ruleset.Map, setup);
        var random = new DeterministicRandom(setup.Seed);

        // Two players keep the original order (spawns, then terrain around them), so their games stay as they were.
        var spawns = ImmutableArray<HexCoord>.Empty;
        if (setup.Map is null && count > 2)
        {
            (map, spawns) = SpawnPlanner.Place(ruleset, MapGenerator.Generate(ruleset, map, random, []), random, count);
        }

        var units = PlaceUnits(ruleset, setup, map, spawns, random);
        if (setup.Map is null && count == 2)
        {
            map = MapGenerator.Generate(ruleset, map, random, [.. units.Select(static unit => unit.Position)]);
        }

        var state = new GameState
        {
            Version = 0,
            Turn = 1,
            CurrentPlayer = setup.Players[0],
            TurnMode = setup.TurnMode,
            Status = GameStatus.InProgress,
            Map = map,
            Players = [.. setup.Players.Select(static id => new Player(id, PlayerStatus.Active, []))],
            Units = units,
            Cities = ImmutableArray<City>.Empty,
            RngState = random.State,
        };

        return VisibilityCalculator.UpdateExplored(ruleset, state);
    }

    // Each player's starting units in seat order, in a row from their spawn: on a pool map one of their candidates, with three or
    // more players the planned spawn, and with two a tile in their edge's zone.
    private static ImmutableArray<Unit> PlaceUnits(
        Ruleset ruleset, GameSetup setup, GameMap map, ImmutableArray<HexCoord> spawns, DeterministicRandom random)
    {
        var ids = new HashSet<int>();
        var units = ImmutableArray.CreateBuilder<Unit>();
        for (var index = 0; index < setup.Players.Length; index++)
        {
            var spawn = setup.Map is { } pool ? PredefinedSpawn(pool, random, index)
                : spawns.IsEmpty ? GeneratedSpawn(ruleset, map, random, isLeftSide: index == 0)
                : spawns[index];
            var tiles = SpawnPlanner.Row(ruleset, map, spawn);
            for (var slot = 0; slot < tiles.Length; slot++)
            {
                var definition = ruleset.Unit(ruleset.Setup.StartingUnits[slot]);
                units.Add(NewUnit(random, ids, setup.Players[index], definition, tiles[slot]));
            }
        }

        return units.ToImmutable();
    }

    private static GameMap CreateMap(MapRules rules, GameSetup setup)
    {
        var size = rules.SizeFor(setup.Players.Length);
        var width = setup.Width ?? size.Width;
        var height = setup.Height ?? size.Height;
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

    private static void ValidateSetup(SetupRules rules, GameSetup setup)
    {
        if (!Enum.IsDefined(setup.TurnMode))
        {
            throw new ArgumentException("Unknown turn mode.", nameof(setup));
        }

        var players = setup.Players;
        if (players.IsDefault || !rules.Allows(players.Length))
        {
            throw new ArgumentException($"A game needs {rules.MinPlayers} to {rules.MaxPlayers} players.", nameof(setup));
        }

        if (setup.Map is { } map && map.Players != players.Length)
        {
            throw new ArgumentException($"The map '{map.Key}' is made for {map.Players} players.", nameof(setup));
        }

        if (players.Any(static player => player.Value == Guid.Empty) || players.Distinct().Count() != players.Length)
        {
            throw new ArgumentException("Players must be distinct and non-empty.", nameof(setup));
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

    // One of the player's candidates on the map; the map's validator keeps the row of starting units passable and on the spawn's
    // side of the centre. The pick is visible to its player, so it is a hashed draw.
    private static HexCoord PredefinedSpawn(PredefinedMap map, DeterministicRandom random, int seat)
    {
        var spawns = map.Spawns[seat];
        return spawns[random.NextHashed(0, spawns.Length)];
    }
}
