using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardSceneBuilderTests
{
    [Fact]
    public void Build_View_HasEveryTileInViewOrderWithItsCoordinate()
    {
        var view = Start();

        var scene = BoardSceneBuilder.Build(view);

        scene.Tiles.Count.ShouldBe(Width * Height);
        scene.Tiles.Select(static tile => new HexCoord(tile.Q, tile.R)).ShouldBe(TestViews.AllCoords());
    }

    [Fact]
    public void Build_Positions_AreTheHexLayoutScaledToOneUnitPerCorner()
    {
        var scene = BoardSceneBuilder.Build(Start());

        var tile = scene.Tiles.Single(tile => new HexCoord(tile.Q, tile.R) == At(3, 2));
        var (x, y) = HexLayout.Center(At(3, 2));
        tile.X.ShouldBe(x / HexLayout.Size, 1e-9);
        tile.Z.ShouldBe(y / HexLayout.Size, 1e-9);
    }

    [Fact]
    public void Build_Visibility_IsSentAsTextAndUnexploredTilesHaveNoTerrain()
    {
        var view = Create(visibility: static coord => coord.R switch
        {
            0 => TileVisibility.Unexplored,
            1 => TileVisibility.Explored,
            _ => TileVisibility.Visible,
        });

        var scene = BoardSceneBuilder.Build(view);

        scene.Tiles.Count(static tile => tile.Visibility == "unexplored").ShouldBe(Width);
        scene.Tiles.Count(static tile => tile.Visibility == "explored").ShouldBe(Width);
        scene.Tiles.Where(static tile => tile.Visibility == "unexplored").ShouldAllBe(static tile => tile.Terrain == null && tile.Shape == "flat");
        scene.Tiles.Where(static tile => tile.Visibility == "unexplored")
            .ShouldAllBe(static tile => tile.Height == TerrainReliefs.Unexplored.Height);
    }

    [Fact]
    public void Build_Terrain_TakesItsRelief()
    {
        var view = Create(terrain: static coord => coord == At(4, 4) ? Forest : coord == At(5, 4) ? Mountains : Plains);

        var scene = BoardSceneBuilder.Build(view);

        var forest = scene.Tiles.Single(tile => new HexCoord(tile.Q, tile.R) == At(4, 4));
        forest.Terrain.ShouldBe("forest");
        forest.Shape.ShouldBe("trees");
        forest.Height.ShouldBe(TerrainReliefs.For(Forest).Height);
        scene.Tiles.Single(tile => new HexCoord(tile.Q, tile.R) == At(5, 4)).Shape.ShouldBe("peaks");
    }

    [Fact]
    public void Build_OwnPieces_OpenTheBoardAroundThemInsteadOfTheWholeMap()
    {
        var scene = BoardSceneBuilder.Build(Create([Settler(1, You, At(2, 2)), Soldier(2, Opponent, At(7, 5))]));

        scene.Start.ShouldNotBe(scene.Map);
        var (x, z) = BoardSceneBuilder.Center(At(2, 2));
        x.ShouldBeInRange(scene.Start.Left, scene.Start.Left + scene.Start.Width);
        z.ShouldBeInRange(scene.Start.Top, scene.Start.Top + scene.Start.Height);
    }

    [Fact]
    public void Build_NoOwnPieces_OpensOnTheWholeMap()
    {
        var scene = BoardSceneBuilder.Build(Create([Soldier(2, Opponent, At(7, 5))]));

        scene.Start.ShouldBe(scene.Map);
        scene.Map.Width.ShouldBe(HexLayout.Bounds(Width, Height).Width / HexLayout.Size, 1e-9);
    }
}
