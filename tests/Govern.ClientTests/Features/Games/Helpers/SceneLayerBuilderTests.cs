using Govern.Client.Features.Games.Helpers;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class SceneLayerBuilderTests
{
    [Fact]
    public void Rivers_RiverEdge_AppearsOnceAlongTheEdgeWithItsWidth()
    {
        var view = Create(rivers: static coord => coord == At(3, 2) ? new TileRivers(East: DeepRiver) : null);

        var river = SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem();

        var (x1, y1, x2, y2) = HexLayout.EdgeEnds(new HexEdge(At(3, 2), HexSide.East));
        river.X1.ShouldBe(x1 / HexLayout.Size, 1e-9);
        river.Z1.ShouldBe(y1 / HexLayout.Size, 1e-9);
        river.X2.ShouldBe(x2 / HexLayout.Size, 1e-9);
        river.Z2.ShouldBe(y2 / HexLayout.Size, 1e-9);
        river.Width.ShouldBe(RiverReliefs.For(DeepRiver).Width);
        river.Deep.ShouldBeTrue();
        river.Visibility.ShouldBe("visible");
    }

    [Fact]
    public void Rivers_BetweenTilesOfDifferentHeights_SitAtTheLowerOne()
    {
        var view = Create(
            terrain: static coord => coord == At(3, 2) ? Hills : Plains,
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);

        SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.For(Plains).Height);
    }

    [Fact]
    public void Rivers_ExploredTile_KeepsItsRiverFaded()
    {
        var view = Create(
            visibility: static coord => coord == At(3, 2) ? TileVisibility.Explored : TileVisibility.Visible,
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: StreamRiver) : null);

        SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem().Visibility.ShouldBe("explored");
    }

    [Fact]
    public void Bridges_BridgeInSight_RunsAcrossItsEdgeBetweenBothTiles()
    {
        var view = Create(
            terrain: static coord => coord == At(4, 2) ? Hills : Plains,
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null,
            bridges: static coord => coord == At(3, 2) ? new TileBridges(East: true) : null);

        var bridge = SceneLayerBuilder.Bridges(view).ShouldHaveSingleItem();

        var (ax, az) = BoardSceneBuilder.Center(At(3, 2));
        var (bx, bz) = BoardSceneBuilder.Center(At(4, 2));
        bridge.X1.ShouldBeInRange(ax, (ax + bx) / 2);
        bridge.X2.ShouldBeInRange((ax + bx) / 2, bx);
        bridge.Z1.ShouldBe(az, 1e-9);
        bridge.Z2.ShouldBe(bz, 1e-9);
        bridge.Y1.ShouldBe(TerrainReliefs.For(Plains).Height);
        bridge.Y2.ShouldBe(TerrainReliefs.For(Hills).Height);
    }

    [Fact]
    public void Bridges_NoBridgeInSight_AreEmpty() => SceneLayerBuilder.Bridges(Start()).ShouldBeEmpty();

    [Fact]
    public void Features_Improvement_StandsInTheTilesUpperLeftWithoutASeat()
    {
        var view = Create(improvements: static coord => coord == At(2, 3) ? Farm : null);

        var farm = SceneLayerBuilder.Features(view).ShouldHaveSingleItem();

        var (x, z) = BoardSceneBuilder.Center(At(2, 3));
        (farm.Q, farm.R).ShouldBe((At(2, 3).Q, At(2, 3).R));
        farm.X.ShouldBeLessThan(x);
        farm.Z.ShouldBeLessThan(z);
        farm.Model.ShouldBe("field");
        farm.Seat.ShouldBe(0);
        farm.Planned.ShouldBeFalse();
    }

    [Fact]
    public void Features_Buildings_TakeTheirOwnersSeatAndAProducedOneIsPlanned()
    {
        var mine = City(7, You, At(2, 2)) with { ProducingBuilding = Campus, ProducingBuildingSite = At(3, 2) };
        var theirs = City(8, Opponent, At(6, 4));
        var view = Create(cities: [mine, theirs], buildings: static coord => coord == At(6, 3) ? Harbour : null);

        var features = SceneLayerBuilder.Features(view);

        features.Count.ShouldBe(2);
        var harbour = features.Single(static feature => feature.Model == "pier");
        harbour.Seat.ShouldBe(2);
        harbour.Planned.ShouldBeFalse();
        var campus = features.Single(static feature => feature.Model == "dome");
        campus.Seat.ShouldBe(1);
        campus.Planned.ShouldBeTrue();
        (campus.Q, campus.R).ShouldBe((At(3, 2).Q, At(3, 2).R));
    }

    [Fact]
    public void Borders_Territory_RunAlongItsOuterEdgesOnlyInTheOwnersSeat()
    {
        var view = Create(cities: [City(7, You, At(3, 3)), City(8, Opponent, At(6, 3))]);

        var borders = SceneLayerBuilder.Borders(view);

        // Each territory is a city and its six neighbours: 18 outer edges each (the two don't touch).
        borders.Count(static border => border.Seat == 1).ShouldBe(18);
        borders.Count(static border => border.Seat == 2).ShouldBe(18);
        var (cx, cz) = BoardSceneBuilder.Center(At(3, 3));
        borders.Where(static border => border.Seat == 1)
            .ShouldAllBe(border => Math.Abs(((border.X1 + border.X2) / 2) - cx) + Math.Abs(((border.Z1 + border.Z2) / 2) - cz) > 1);
    }

    [Fact]
    public void Borders_NoTerritory_AreEmpty() => SceneLayerBuilder.Borders(Start()).ShouldBeEmpty();

    [Fact]
    public void Rivers_NextToAnUnexploredTile_SitAtTheKnownTile()
    {
        var view = Create(
            terrain: static coord => coord == At(3, 2) ? Hills : Plains,
            visibility: static coord => coord == At(4, 2) ? TileVisibility.Unexplored : TileVisibility.Visible,
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);

        SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.For(Hills).Height);
    }

    [Fact]
    public void Rivers_OnTheMapsRim_SitAtTheirOneTile()
    {
        var view = Create(
            terrain: static coord => coord == At(Width - 1, 2) ? Hills : Plains,
            rivers: static coord => coord == At(Width - 1, 2) ? new TileRivers(East: River) : null,
            bridges: static coord => coord == At(Width - 1, 2) ? new TileBridges(East: true) : null);

        SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.For(Hills).Height);
        var bridge = SceneLayerBuilder.Bridges(view).ShouldHaveSingleItem();
        bridge.Y2.ShouldBe(bridge.Y1);
    }

    [Fact]
    public void Layers_OnALake_StandOnTheWater()
    {
        var view = Create(
            cities: [City(7, You, At(3, 3))],
            terrain: static coord => coord == At(2, 3) ? Lake : Plains,
            buildings: static coord => coord == At(2, 3) ? Harbour : null,
            rivers: static coord => coord == At(2, 3) ? new TileRivers(East: River) : null);

        SceneLayerBuilder.Features(view).ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.WaterLevel);
        SceneLayerBuilder.Rivers(view).ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.For(Plains).Height);
        SceneLayerBuilder.Borders(view)
            .Where(static border => border.Height == TerrainReliefs.WaterLevel)
            .ShouldNotBeEmpty("the lake tile's border stretches lie on its water");
    }

    [Fact]
    public void Features_ProducingSiteOutsideTheMap_IsLeftOut()
    {
        var city = City(7, You, At(2, 2)) with { ProducingBuilding = Campus, ProducingBuildingSite = new HexCoord(-50, -50) };

        SceneLayerBuilder.Features(Create(cities: [city])).ShouldBeEmpty();
    }
}
