using System.Collections.Immutable;
using Govern.Core.Engine;
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
        var state = GameFactory.Create(new GameSetup([P1, P2], Seed: 1));

        state.Version.ShouldBe(0);
        state.Turn.ShouldBe(1);
        state.CurrentPlayer.ShouldBe(P1);
        state.Status.ShouldBe(GameStatus.InProgress);
        state.Winner.ShouldBeNull();
        state.Cities.ShouldBeEmpty();
        state.Map.Width.ShouldBe(GameSetup.DefaultWidth);
        state.Map.Height.ShouldBe(GameSetup.DefaultHeight);
    }

    [Fact]
    public void Create_SameSeed_DrawsTheSameIds()
    {
        var first = GameFactory.Create(new GameSetup([P1, P2], Seed: 11));
        var second = GameFactory.Create(new GameSetup([P1, P2], Seed: 11));

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

        var ids = GameFactory.Create(new GameSetup([P1, P2], seed)).Units.Where(unit => unit.Owner == P1).Select(unit => unit.Id.Value).ToList();

        ids.ShouldNotContain(rawSettlerDraw);
        ids.ShouldNotContain(rawSoldierDraw);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(987_654_321UL)]
    public void Create_AnySeed_DrawsDistinctPositiveIdsThatDoNotCountUnits(ulong seed)
    {
        var ids = GameFactory.Create(new GameSetup([P1, P2], seed)).Units.Select(static unit => unit.Id.Value).ToList();

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
        var state = GameFactory.Create(new GameSetup([P1, P2], seed));
        var quarter = state.Map.Width / 4;

        foreach (var player in new[] { P1, P2 })
        {
            var units = state.Units.Where(unit => unit.Owner == player).ToList();
            var settler = units.Single(unit => unit.Type == UnitType.Settler);
            var soldier = units.Single(unit => unit.Type == UnitType.Soldier);

            units.Count.ShouldBe(2);
            settler.Position.DistanceTo(soldier.Position).ShouldBe(1);
            units.ShouldAllBe(unit => state.Map.Contains(unit.Position) && unit.MovementLeft == unit.Definition.Movement);
        }

        state.Units.Where(unit => unit.Owner == P1).ShouldAllBe(unit => unit.Position.Column <= quarter);
        state.Units.Where(unit => unit.Owner == P2).ShouldAllBe(unit => unit.Position.Column >= state.Map.Width - quarter - 1);
    }

    [Fact]
    public void Create_Always_ExploresAroundEachPlayersOwnUnitsOnly()
    {
        var state = GameFactory.Create(new GameSetup([P1, P2], Seed: 5));

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

        GameSerializer.Serialize(GameFactory.Create(setup)).ShouldBe(GameSerializer.Serialize(GameFactory.Create(setup)));
    }

    [Fact]
    public void Create_DifferentSeeds_ProduceDifferentSpawns()
    {
        var spawns = Enumerable.Range(1, 20)
            .Select(seed => GameFactory.Create(new GameSetup([P1, P2], (ulong)seed)))
            .Select(state => string.Join(';', state.Units.Select(unit => unit.Position)))
            .Distinct();

        spawns.Count().ShouldBeGreaterThan(1);
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
        Should.Throw<ArgumentException>(() => GameFactory.Create(new GameSetup(players, Seed: 1)));
    }
}
