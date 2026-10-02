using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class TileCardBuilderTests
{
    // The tile east of (3,2): the edge (3,2) owns on its east side lies between them.
    private static readonly HexCoord EastOfRiver = new HexEdge(At(3, 2), HexSide.East).Neighbor();

    [Fact]
    public void For_PlainTile_IsItsTerrainAlone()
    {
        var card = TileCardBuilder.For(Ruleset, Create(), At(1, 4), null).ShouldNotBeNull();

        card.ShouldBe(card with { Tile = At(1, 4), Title = "Plains" });
        card.Lines.ShouldBeEmpty();
        card.Text.ShouldBe("Plains.");
    }

    [Fact]
    public void For_RiverAndBridge_AreToldOnBothSidesOfTheEdge()
    {
        var view = Create(
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null,
            bridges: static coord => coord == At(3, 2) ? new TileBridges(East: true) : null);

        foreach (var tile in new[] { At(3, 2), EastOfRiver })
        {
            TileCardBuilder.For(Ruleset, view, tile, null).ShouldNotBeNull().Lines
                .ShouldBe([RiverDescription.For(Ruleset, River), "Bridge over the river"]);
        }
    }

    [Fact]
    public void For_ImprovementAndBuildings_NameThemAndMarkOneBeingBuilt()
    {
        var city = City(7, You, At(5, 4)) with { ProducingBuilding = Harbour, ProducingBuildingSite = At(1, 1) };
        var view = Create(
            cities: [city],
            improvements: static coord => coord == At(1, 4) ? Farm : null,
            buildings: static coord => coord == At(6, 4) ? Campus : null);

        TileCardBuilder.For(Ruleset, view, At(1, 4), null)!.Lines.ShouldBe(["Improvement: Farm"]);
        TileCardBuilder.For(Ruleset, view, At(6, 4), null)!.Lines.ShouldBe(["Building: Campus", "Territory of City 1"]);
        TileCardBuilder.For(Ruleset, view, At(1, 1), null)!.Lines.ShouldBe([$"Building: {Ruleset.BuildingName(Harbour)} (being built)"]);
    }

    [Fact]
    public void For_CitiesAndUnits_SayWhoseTheyAre()
    {
        var view = Create(
            [Soldier(2, You, At(5, 4), health: 40), Soldier(3, Opponent, At(2, 1))],
            [City(7, You, At(5, 4), name: "Kyiv"), City(8, Opponent, At(2, 1), name: "Lviv")]);

        TileCardBuilder.For(Ruleset, view, At(5, 4), null)!.Lines
            .ShouldBe(["Your city: Kyiv", "Your unit: Soldier, health 40 of 100", "Territory of Kyiv"]);
        TileCardBuilder.For(Ruleset, view, At(2, 1), null)!.Lines
            .ShouldBe(["Enemy city: Lviv", "Enemy unit: Soldier, health 100 of 100", "Enemy territory"]);
    }

    [Fact]
    public void For_Lens_EndsWithTheTilesAndEveryEdgesSentence()
    {
        var lens = new MovementCostLens(new RulesetService());
        var view = Create(rivers: static coord => coord == At(3, 2) ? new TileRivers(East: DeepRiver) : null);
        var edge = lens.EdgeMarksFor(view.TileAt(At(3, 2))).ShouldHaveSingleItem().Description;

        var owner = TileCardBuilder.For(Ruleset, view, At(3, 2), lens)!;
        var neighbour = TileCardBuilder.For(Ruleset, view, EastOfRiver, lens)!;

        owner.Lines.ShouldBe([RiverDescription.For(Ruleset, DeepRiver), "Plains: costs 1 movement to enter", edge]);
        neighbour.Lines[^1].ShouldBe(edge);
        TileCardBuilder.For(Ruleset, view, At(1, 4), lens)!.Lines.ShouldBe(["Plains: costs 1 movement to enter"]);
    }

    [Fact]
    public void For_TileOutOfSight_SaysSoAndShowsNoPieces()
    {
        var view = Create([Soldier(3, Opponent, At(4, 4))], visibility: static _ => TileVisibility.Explored) with { Units = [] };

        TileCardBuilder.For(Ruleset, view, At(4, 4), null)!.Lines.ShouldBe(["Out of sight"]);
    }

    [Fact]
    public void For_UnexploredOrOffTheMap_IsNull()
    {
        TileCardBuilder.For(Ruleset, Create(visibility: static _ => TileVisibility.Unexplored), At(1, 1), null).ShouldBeNull();
        TileCardBuilder.For(Ruleset, Create(), new HexCoord(-5, -5), null).ShouldBeNull();
    }
}
