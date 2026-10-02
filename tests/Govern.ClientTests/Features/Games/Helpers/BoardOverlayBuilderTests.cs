using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.Units;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardOverlayBuilderTests
{
    [Fact]
    public void Build_NothingSelectedNoLens_IsNone() =>
        BoardOverlayBuilder.Build(Start(), BoardSelection.None, BoardHighlights.None, null).ShouldBe(BoardOverlay.None);

    [Fact]
    public void Build_Highlights_TakeTheirTintAndTheTargetItsOwn()
    {
        var highlights = new BoardHighlights([At(1, 1)], [At(4, 2), At(5, 2)], [At(2, 1)], [At(3, 3)]);
        var selection = BoardSelection.Of(new UnitId(2)) with { Target = At(5, 2) };

        var overlay = BoardOverlayBuilder.Build(Start(), selection, highlights, null);

        overlay.Tiles.Select(static tile => tile.Tint).ShouldBe(
            ["--gv-overlay-reachable", "--gv-overlay-attackable", "--gv-overlay-target", "--gv-overlay-bridgeable", "--gv-overlay-placeable"]);
        var (x, z) = BoardSceneBuilder.Center(At(5, 2));
        overlay.Tiles[2].X.ShouldBe(x);
        overlay.Tiles[2].Z.ShouldBe(z);
    }

    [Fact]
    public void Build_SelectedUnitOrCity_RingsItsTile()
    {
        var view = Create([Soldier(2, You, At(3, 2))], [City(7, You, At(5, 4))]);

        var unit = BoardOverlayBuilder.Build(view, BoardSelection.Of(new UnitId(2)), BoardHighlights.None, null).Selected.ShouldNotBeNull();
        var city = BoardOverlayBuilder.Build(view, BoardSelection.Of(new CityId(7)), BoardHighlights.None, null).Selected.ShouldNotBeNull();

        (unit.X, unit.Z).ShouldBe(BoardSceneBuilder.Center(At(3, 2)));
        (city.X, city.Z).ShouldBe(BoardSceneBuilder.Center(At(5, 4)));
        unit.Tint.ShouldBe("--gv-selected");
    }

    [Fact]
    public void Build_SelectionNoLongerInTheView_RingsNothing() =>
        BoardOverlayBuilder.Build(Start(), BoardSelection.Of(new UnitId(99)), BoardHighlights.None, null).Selected.ShouldBeNull();

    [Fact]
    public void Build_Lens_MarksTilesWithLabelsAndEdgesWithBadgesBeforeHighlights()
    {
        var view = Create(rivers: static coord => coord == At(3, 2) ? new TileRivers(East: DeepRiver) : null);
        var highlights = BoardHighlights.None with { Reachable = [At(1, 1)] };

        var overlay = BoardOverlayBuilder.Build(view, BoardSelection.None, highlights, new MovementCostLens(new RulesetService()));

        overlay.Tiles.Count.ShouldBe((Width * Height) + 1);
        overlay.Tiles.Take(Width * Height).ShouldAllBe(static tile => tile.Tint.StartsWith("--gv-lens-") && tile.Label != null);
        overlay.Tiles[^1].Tint.ShouldBe("--gv-overlay-reachable");
        var badge = overlay.Edges.ShouldHaveSingleItem();
        badge.Mark.ShouldBe("blocked");
        var (x, y) = HexLayout.EdgeMiddle(new HexEdge(At(3, 2), HexSide.East));
        badge.X.ShouldBe(x / HexLayout.Size, 1e-9);
        badge.Z.ShouldBe(y / HexLayout.Size, 1e-9);
    }
}
