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
        state.Map.Width.ShouldBe(TestRulesets.Default.Map.DefaultWidth);
        state.Map.Height.ShouldBe(TestRulesets.Default.Map.DefaultHeight);
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

    // Recorded with ruleset v2 (G4 map generation): the same ruleset and setup must always give exactly the same new game.
    [Theory]
    [InlineData(1UL, "23c358634f56c5b0c33ea29ea9fb6f8b7584f4528facd1b511a91ce9a3218f9a")]
    [InlineData(42UL, "e6746696b02ed70baff27a2b464dc09ceef3224c47346ef69e4e5093592e8ec5")]
    [InlineData(987_654_321UL, "a37bc4ec8a0d9f8b8fc0aae14c1d0611ac43e89c18ef598b5ce23f94806c39e4")]
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
            json["map"]!["defaultWidth"] = 24;
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
            json["terrains"]!.AsArray().Add(new JsonObject { ["id"] = "grass", ["movementCost"] = 1, ["isPassable"] = true, ["sightBonus"] = 0 });
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
        ImmutableArray.Create(P1, P2, new PlayerId(Guid.NewGuid())),
        ImmutableArray.Create(P1, P1),
        ImmutableArray.Create(P1, new PlayerId(Guid.Empty)),
    };

    [Theory]
    [MemberData(nameof(InvalidPlayers))]
    public void Create_InvalidPlayers_Throws(ImmutableArray<PlayerId> players)
    {
        Should.Throw<ArgumentException>(() => GameFactory.Create(TestRulesets.Default, new GameSetup(players, Seed: 1)));
    }
}
