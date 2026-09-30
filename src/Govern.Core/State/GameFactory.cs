using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Core.State;

/// <summary>
/// Creates the initial <see cref="GameState"/> from the game's ruleset: a generated map (forests, hills and so on), and for
/// each player its starting units. The first player spawns in a zone at the left edge of the map, the second at the right
/// edge; the seed picks the exact spawn tile, and the other starting units stand in a row from it towards the centre. The
/// map's two halves are mirrors of each other, but the spawns are not, so the map barely hints where the opponent started.
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
    public static GameState Create(Ruleset ruleset, GameSetup setup)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(setup);
        ValidatePlayers(ruleset.Setup.Players, setup.Players);

        var blank = CreateMap(ruleset.Map, setup);
        var random = new DeterministicRandom(setup.Seed);
        var ids = new HashSet<int>();
        var units = ImmutableArray.CreateBuilder<Unit>();

        for (var index = 0; index < setup.Players.Length; index++)
        {
            var tiles = SpawnTiles(ruleset, blank, random, isLeftSide: index == 0);
            for (var slot = 0; slot < tiles.Length; slot++)
            {
                var definition = ruleset.Unit(ruleset.Setup.StartingUnits[slot]);
                units.Add(NewUnit(random, ids, setup.Players[index], definition, tiles[slot]));
            }
        }

        var map = MapGenerator.Generate(ruleset, blank, random, [.. units.Select(static unit => unit.Position)]);

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

    // The spawn tile lies in the player's zone at their edge of the map; the k-th starting unit stands k columns from it towards
    // the centre. The validator keeps every starting unit on the map and on its own side.
    private static ImmutableArray<HexCoord> SpawnTiles(Ruleset ruleset, GameMap map, DeterministicRandom random, bool isLeftSide)
    {
        var spawn = ruleset.Map.Spawn;
        var zone = spawn.ZoneColumns(map.Width);
        var column = isLeftSide ? random.Next(spawn.EdgeMargin, zone) : random.Next(map.Width - zone, map.Width - spawn.EdgeMargin);
        var row = random.Next(spawn.EdgeMargin, map.Height - spawn.EdgeMargin);
        var towardsCentre = isLeftSide ? 1 : -1;

        return [.. Enumerable.Range(0, ruleset.Setup.StartingUnits.Length)
            .Select(slot => HexCoord.FromOffset(column + (slot * towardsCentre), row))];
    }
}
