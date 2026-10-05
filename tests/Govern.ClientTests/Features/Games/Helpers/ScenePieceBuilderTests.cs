using Govern.Client.Features.Games.Helpers;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class ScenePieceBuilderTests
{
    [Fact]
    public void Pieces_Units_TakeTheirModelSeatOwnerAndIcon()
    {
        var pieces = ScenePieceBuilder.Pieces(Start(), 100);

        pieces.Count.ShouldBe(3);
        var settler = pieces.Single(static piece => piece.Model == "settler");
        (settler.Kind, settler.Seat, settler.Own, settler.Icon, settler.Health).ShouldBe(("unit", 1, true, "unit-settler", 1.0));
        var enemy = pieces.Single(piece => !piece.Own);
        (enemy.Model, enemy.Seat).ShouldBe(("soldier", 2));
        var (x, z) = BoardSceneBuilder.Center(At(2, 2));
        (settler.X, settler.Z).ShouldBe((x, z));
    }

    [Fact]
    public void Pieces_HurtUnit_CarriesItsHealthShare() =>
        ScenePieceBuilder.Pieces(Create([Soldier(2, You, At(3, 2), health: 25)]), 100).ShouldHaveSingleItem().Health.ShouldBe(0.25);

    [Fact]
    public void Pieces_NoMaxHealth_TreatsUnitsAsUnhurt() =>
        ScenePieceBuilder.Pieces(Create([Soldier(2, You, At(3, 2), health: 25)]), 0).ShouldHaveSingleItem().Health.ShouldBe(1);

    [Fact]
    public void Pieces_City_ComesFirstWithItsNameAndAUnitThereStandsInFront()
    {
        var pieces = ScenePieceBuilder.Pieces(Create([Soldier(2, You, At(3, 3))], [City(7, You, At(3, 3), "Kyiv")]), 100);

        var city = pieces[0];
        (city.Kind, city.Model, city.Name, city.Icon).ShouldBe(("city", "city", "Kyiv", "city"));
        var unit = pieces[1];
        unit.X.ShouldBeGreaterThan(city.X);
        unit.Z.ShouldBeGreaterThan(city.Z);
    }

    [Fact]
    public void Pieces_Units_CarryTheirIdAndCitiesNone()
    {
        var pieces = ScenePieceBuilder.Pieces(Create([Soldier(2, You, At(3, 3))], [City(7, You, At(3, 3), "Kyiv")]), 100);

        pieces.Select(static piece => piece.Unit).ShouldBe([null, 2]);
    }

    [Fact]
    public void Ghost_UnitOutOfTheView_StandsOnItsTileWithItsModelAndSeat()
    {
        var ghost = ScenePieceBuilder.Ghost(Start(), At(4, 4), ArcherType, Opponent);

        (ghost.Q, ghost.R, ghost.Model, ghost.Seat, ghost.Own, ghost.Icon, ghost.Unit).ShouldBe((At(4, 4).Q, At(4, 4).R, "archer", 2, false, "unit-archer", (int?)null));
    }

    [Fact]
    public void Pieces_UnitsSharingATile_StandSideBySide()
    {
        var pieces = ScenePieceBuilder.Pieces(Create([Soldier(2, You, At(3, 3)), Unit(4, You, TraderType, At(3, 3))]), 100);

        var (x, z) = BoardSceneBuilder.Center(At(3, 3));
        pieces.Select(static piece => piece.Z).ShouldAllBe(pieceZ => pieceZ == z);
        pieces[0].X.ShouldBeLessThan(x);
        pieces[1].X.ShouldBeGreaterThan(x);
        (pieces[0].X + pieces[1].X).ShouldBe(2 * x, 1e-9);
    }
}
