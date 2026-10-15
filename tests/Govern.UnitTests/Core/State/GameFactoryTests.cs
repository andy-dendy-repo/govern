using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Core.State;

public class GameFactoryTests
{
    private static readonly PlayerId P1 = GameStateBuilder.P1;
    private static readonly PlayerId P2 = GameStateBuilder.P2;

    [Fact]
    public void Create_TwoPlayers_StartsTurnOneWithTheFirstPlayer()
    {
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 1));

        state.Version.ShouldBe(0);
        state.Turn.ShouldBe(1);
        state.CurrentPlayer.ShouldBe(P1);
        state.Status.ShouldBe(GameStatus.InProgress);
        state.Winner.ShouldBeNull();
        state.Cities.ShouldBeEmpty();
        state.Map.Width.ShouldBe(TestRulesets.Default.Map.SizeFor(2).Width);
        state.Map.Height.ShouldBe(TestRulesets.Default.Map.SizeFor(2).Height);
    }

    [Fact]
    public void Create_SameSeed_DrawsTheSameIds()
    {
        var first = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 11));
        var second = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 11));

        second.Units.Select(static unit => unit.Id).ShouldBe(first.Units.Select(static unit => unit.Id));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(987_654_321UL)]
    public void Create_Ids_AreNotRawGeneratorDraws(ulong seed)
    {
        // Draw order per player: spawn column, spawn row, settler id, soldier id. Raw SplitMix64 draws are invertible, so an
        // id equal to one would let a player rebuild the generator state (and the opponent's hidden spawn).
        var random = new DeterministicRandom(seed);
        random.Next(0, 2);
        random.Next(0, 2);
        var rawSettlerDraw = random.Next(1, int.MaxValue);
        var rawSoldierDraw = random.Next(1, int.MaxValue);

        var ids = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], seed)).Units.Where(unit => unit.Owner == P1).Select(unit => unit.Id.Value).ToList();

        ids.ShouldNotContain(rawSettlerDraw);
        ids.ShouldNotContain(rawSoldierDraw);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(987_654_321UL)]
    public void Create_AnySeed_DrawsDistinctPositiveIdsThatDoNotCountUnits(ulong seed)
    {
        var ids = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], seed)).Units.Select(static unit => unit.Id.Value).ToList();

        ids.ShouldBeUnique();
        ids.ShouldAllBe(id => id > 0);
        ids.Order().SequenceEqual(Enumerable.Range(1, ids.Count)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(987_654_321UL)]
    public void Create_AnySeed_GivesEachPlayerAnAdjacentSettlerAndSoldierOnTheirSide(ulong seed)
    {
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], seed));
        var quarter = state.Map.Width / 4;

        foreach (var player in new[] { P1, P2 })
        {
            var units = state.Units.Where(unit => unit.Owner == player).ToList();
            var settler = units.Single(unit => unit.Type == TestRulesets.Settler);
            var soldier = units.Single(unit => unit.Type == TestRulesets.Soldier);

            units.Count.ShouldBe(2);
            settler.Position.DistanceTo(soldier.Position).ShouldBe(1);
            units.ShouldAllBe(unit => state.Map.Contains(unit.Position) && unit.MovementLeft == TestRulesets.Default.Unit(unit.Type).Movement);
        }

        state.Units.Where(unit => unit.Owner == P1).ShouldAllBe(unit => unit.Position.Column <= quarter);
        state.Units.Where(unit => unit.Owner == P2).ShouldAllBe(unit => unit.Position.Column >= state.Map.Width - quarter - 1);
    }

    [Fact]
    public void Create_Always_ExploresAroundEachPlayersOwnUnitsOnly()
    {
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 5));

        foreach (var player in state.Players)
        {
            var own = state.Units.Where(unit => unit.Owner == player.Id).Select(unit => unit.Position);
            var enemy = state.Units.Where(unit => unit.Owner != player.Id).Select(unit => unit.Position);

            player.Explored.ShouldBeSubsetOf(state.Map.Tiles.Select(tile => tile.Coord));
            own.ShouldBeSubsetOf(player.Explored);
            player.Explored.ShouldNotContain(coord => enemy.Contains(coord));
        }
    }

    [Fact]
    public void Create_SameSetup_ProducesIdenticalState()
    {
        var setup = new GameSetup([P1, P2], Seed: 42);

        GameSerializer.Serialize(GameFactory.Create(TestRulesets.Default, setup)).ShouldBe(GameSerializer.Serialize(GameFactory.Create(TestRulesets.Default, setup)));
    }

    // Recorded with ruleset v6 (G5/G7 desert patches): the same ruleset and setup must always give exactly the same new game.
    [Theory]
    [InlineData(1UL, "bd1308812ebb11f60ee82adc198489f88fa49a698b7ce4ae73d724b059a374bf")]
    [InlineData(42UL, "1ba44705f475dbacc8245d935676e3cacba901783c002cc65668c08de152822a")]
    [InlineData(987_654_321UL, "67698a82486d31f0ac75f542da893ed970e9087977fb6d41dc0a1d6228746281")]
    public void Create_KnownSeed_GivesTheRecordedState(ulong seed, string expectedSha256)
    {
        var json = GameSerializer.Serialize(GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], seed)));

        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ShouldBe(expectedSha256);
    }

    [Fact]
    public void Create_DifferentSeeds_ProduceDifferentSpawns()
    {
        var spawns = Enumerable.Range(1, 20)
            .Select(seed => GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], (ulong)seed)))
            .Select(state => string.Join(';', state.Units.Select(unit => unit.Position)))
            .Distinct();

        spawns.Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Create_RulesetStartingUnits_StandInARowTowardsTheCentre()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["setup"]!["startingUnits"] = new JsonArray("settler", "soldier", "soldier");
            json["map"]!["minSize"] = 12;
            json["map"]!["sizes"]![0]!["width"] = 24;
        });

        var state = GameFactory.Create(ruleset, new GameSetup([P1, P2], Seed: 3));

        foreach (var (player, step) in new[] { (P1, 1), (P2, -1) })
        {
            var units = state.Units.Where(unit => unit.Owner == player).ToList();
            units.Select(static unit => unit.Type).ShouldBe([TestRulesets.Settler, TestRulesets.Soldier, TestRulesets.Soldier]);
            units.Select(static unit => unit.Position.Row).Distinct().ShouldHaveSingleItem();
            units.Select(static unit => unit.Position.Column).ShouldBe(
                Enumerable.Range(0, 3).Select(slot => units[0].Position.Column + (slot * step)));
        }

        state.Map.Width.ShouldBe(24);
    }

    [Fact]
    public void Create_SetupSizeAndNoPatches_OverridesTheRulesetDefaultAndFillsTheDefaultTerrain()
    {
        var ruleset = TestRulesets.With(json =>
        {
            json["terrains"]!.AsArray().Add(new JsonObject { ["id"] = "grass", ["movementCost"] = 1, ["isPassable"] = true, ["sightBonus"] = 0, ["defenceBonus"] = 0, ["yields"] = new JsonArray() });
            json["texts"]!["terrain.grass.name"] = "Grass";
            json["map"]!["defaultTerrain"] = "grass";
            json["map"]!["generation"]!["patches"] = new JsonArray();
        });

        var state = GameFactory.Create(ruleset, new GameSetup([P1, P2], Seed: 3, Width: 10, Height: 9));

        (state.Map.Width, state.Map.Height).ShouldBe((10, 9));
        state.Map.Tiles.ShouldAllBe(tile => tile.Terrain == new TerrainType("grass"));
    }

    [Theory]
    [InlineData(7, 14)]
    [InlineData(41, 14)]
    [InlineData(20, 7)]
    [InlineData(20, 41)]
    public void Create_SizeOutsideTheRulesetLimits_Throws(int width, int height)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 1, width, height)));
    }

    public static TheoryData<ImmutableArray<PlayerId>> InvalidPlayers => new()
    {
        ImmutableArray<PlayerId>.Empty,
        ImmutableArray.Create(P1),
        ImmutableArray.Create(P1, P2, new PlayerId(Guid.NewGuid()), new PlayerId(Guid.NewGuid()), new PlayerId(Guid.NewGuid())),
        ImmutableArray.Create(P1, P1),
        ImmutableArray.Create(P1, new PlayerId(Guid.Empty)),
    };

    [Theory]
    [MemberData(nameof(InvalidPlayers))]
    public void Create_InvalidPlayers_Throws(ImmutableArray<PlayerId> players)
    {
        Should.Throw<ArgumentException>(() => GameFactory.Create(TestRulesets.Default, new GameSetup(players, Seed: 1)));
    }

    public static TheoryData<int, ulong> ManyPlayers => new() { { 3, 1 }, { 3, 7 }, { 3, 42 }, { 4, 1 }, { 4, 7 }, { 4, 42 } };

    [Theory]
    [MemberData(nameof(ManyPlayers))]
    public void Create_ThreeOrFourPlayers_SpawnsEachInTheirOwnCornerOnPassableLand(int count, ulong seed)
    {
        var players = Players(count);
        var ruleset = TestRulesets.Default;

        var state = GameFactory.Create(ruleset, new GameSetup(players, seed));

        var size = ruleset.Map.SizeFor(count);
        (state.Map.Width, state.Map.Height).ShouldBe((size.Width, size.Height));
        var columns = ruleset.Map.Spawn.ZoneColumns(size.Width);
        var rows = size.Height / 3;
        for (var seat = 0; seat < count; seat++)
        {
            var units = state.Units.Where(unit => unit.Owner == players[seat]).ToList();
            units.Select(static unit => unit.Type).ShouldBe(ruleset.Setup.StartingUnits);
            units.ShouldAllBe(unit => state.Map.IsPassable(ruleset, unit.Position));
            var spawn = units[0].Position;
            var (left, top) = Corner(count, seat);
            (left ? spawn.Column < columns : spawn.Column >= size.Width - columns).ShouldBeTrue($"seat {seat} column {spawn.Column}");
            if (top is { } isTop)
            {
                (isTop ? spawn.Row < rows : spawn.Row >= size.Height - rows).ShouldBeTrue($"seat {seat} row {spawn.Row}");
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Create_ThreeOrFourPlayers_StartsAreWorthAboutTheSame(int count)
    {
        var ruleset = TestRulesets.Default;
        foreach (var seed in Enumerable.Range(1, 30).Select(static seed => (ulong)seed))
        {
            var state = GameFactory.Create(ruleset, new GameSetup(Players(count), seed));

            var values = state.Units.GroupBy(static unit => unit.Owner).Select(group => StartValue(state.Map, group.First().Position)).ToList();
            (values.Max() * 100).ShouldBeLessThanOrEqualTo(values.Min() * 111, $"seed {seed}: {string.Join(", ", values)}");
        }
    }

    [Fact]
    public void Create_ThreePlayers_SameSeedGivesTheSameState()
    {
        var setup = new GameSetup(Players(3), 5);

        GameSerializer.Serialize(GameFactory.Create(TestRulesets.Default, setup)).ShouldBe(GameSerializer.Serialize(GameFactory.Create(TestRulesets.Default, setup)));
    }

    [Fact]
    public void Create_PredefinedMapForTwoWithThreePlayers_Throws()
    {
        var basin = PredefinedMap.ParseValid(TestRulesets.Default, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "maps", "basin.json")));

        Should.Throw<ArgumentException>(() => GameFactory.Create(TestRulesets.Default, new GameSetup(Players(3), 1, Map: basin)));
    }

    private static ImmutableArray<PlayerId> Players(int count) =>
        [.. new[] { P1, P2, GameStateBuilder.P3, GameStateBuilder.P4 }.Take(count)];

    // Whether a seat's zone is on the left, and on the top (null: the whole height, the first of three).
    private static (bool Left, bool? Top) Corner(int count, int seat) => (count, seat) switch
    {
        (3, 0) => (true, null),
        (3, 1) => (false, true),
        (3, 2) => (false, false),
        (_, 0) => (true, true),
        (_, 1) => (false, true),
        (_, 2) => (false, false),
        _ => (true, false),
    };

    // What a start is worth, as the map pool's balance check measures it: the yields of the tiles within 3 tiles of the spawn
    // and of the rivers between them.
    private static int StartValue(GameMap map, HexCoord spawn)
    {
        var ruleset = TestRulesets.Default;
        var area = spawn.WithinRadius(3).Where(map.Contains).ToHashSet();
        var tiles = area.Sum(coord => ruleset.Terrain(map.TileAt(coord).Terrain).Yields.Sum(static amount => amount.Amount));
        var rivers = area
            .SelectMany(coord => coord.Neighbors().Where(area.Contains).Select(next => (coord, next)))
            .Where(static pair => (pair.coord.Row, pair.coord.Column).CompareTo((pair.next.Row, pair.next.Column)) < 0)
            .Sum(pair => map.RiverBetween(pair.coord, pair.next) is { } river ? ruleset.River(river).Yields.Sum(static amount => amount.Amount) : 0);
        return tiles + rivers;
    }
}
