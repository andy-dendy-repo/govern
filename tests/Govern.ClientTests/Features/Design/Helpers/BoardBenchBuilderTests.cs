using Govern.Client.Features.Design.Helpers;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Design.Helpers;

public class BoardBenchBuilderTests
{
    [Fact]
    public void Build_Revealed_ShowsTheLargestMapInSightWithAnArmyCitiesAndRivers()
    {
        var view = BoardBenchBuilder.Build(Ruleset, revealed: true);

        view.Width.ShouldBe(Ruleset.Map.MaxSize);
        view.Height.ShouldBe(Ruleset.Map.MaxSize);
        view.Tiles.Length.ShouldBe(view.Width * view.Height);
        view.Tiles.ShouldAllBe(static tile => tile.Visibility == TileVisibility.Visible);
        view.Tiles.Select(static tile => tile.Terrain).Distinct().Count().ShouldBe(Ruleset.Terrains.Length);
        view.Tiles.ShouldContain(static tile => tile.Rivers != null);
        view.Tiles.ShouldContain(static tile => tile.TerritoryOwner != null);
        view.Units.Length.ShouldBeGreaterThan(100);
        view.Units.Select(static unit => unit.Owner).Distinct().Count().ShouldBe(2);
        view.Units.Select(static unit => unit.Type).Distinct().Count().ShouldBe(Ruleset.Units.Length);
        view.Cities.ShouldNotBeEmpty();
        view.Units.ShouldAllBe(unit => Ruleset.Terrain(view.TileAt(unit.Position).Terrain!.Value).AllowsDomain(Ruleset.Unit(unit.Type).Domain));
    }

    [Fact]
    public void Build_Opening_ShowsThreeUnitsInSightOfACorner()
    {
        var view = BoardBenchBuilder.Build(Ruleset, revealed: false);

        view.Units.Length.ShouldBe(3);
        view.Units.ShouldAllBe(static unit => unit.Owner == BoardBenchBuilder.You);
        view.Cities.ShouldBeEmpty();
        view.Tiles.ShouldContain(static tile => tile.Visibility == TileVisibility.Visible);
        view.Tiles.ShouldContain(static tile => tile.Visibility == TileVisibility.Explored);
        view.Tiles.Count(static tile => tile.Visibility == TileVisibility.Unexplored).ShouldBeGreaterThan(view.Tiles.Length / 2);
    }

    [Fact]
    public void Build_Twice_GivesTheSameView()
    {
        BoardBenchBuilder.Build(Ruleset, revealed: true).ShouldBeEquivalentTo(BoardBenchBuilder.Build(Ruleset, revealed: true));
    }

    [Fact]
    public void March_MovesOnlyTheViewersUnitsEastOntoFreeTilesOfTheirGround()
    {
        var view = BoardBenchBuilder.Build(Ruleset, revealed: true);

        var next = BoardBenchBuilder.March(Ruleset, view);

        next.Version.ShouldBe(view.Version + 1);
        next.Tiles.ShouldBe(view.Tiles);
        var moves = view.Units.Zip(next.Units).ToList();
        moves.Where(static pair => pair.First.Owner == BoardBenchBuilder.Them).ShouldAllBe(static pair => pair.First == pair.Second);
        moves.Where(static pair => pair.First.Position != pair.Second.Position).ShouldNotBeEmpty();
        moves.Where(static pair => pair.First.Position != pair.Second.Position)
            .ShouldAllBe(pair => pair.Second.Position == HexCoord.FromOffset(pair.First.Position.Column + 1, pair.First.Position.Row));
        next.Units.Select(static unit => unit.Position).Distinct().Count().ShouldBe(next.Units.Length);
        next.Units.ShouldAllBe(unit => Ruleset.Terrain(next.TileAt(unit.Position).Terrain!.Value).AllowsDomain(Ruleset.Unit(unit.Type).Domain));
    }
}
