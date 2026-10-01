using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Lenses;

public class YieldLensTests
{
    private readonly YieldLens _lens = new(new RulesetService());

    public static TheoryData<string, string, string, string> KnownTiles => new()
    {
        { "plains", "3", "poor", "Plains: 2 food, 1 water a turn" },
        { "forest", "4", "rich", "Forest: 1 food, 1 water, 2 wood a turn" },
        { "lake", "4", "rich", "Lake: 1 food, 3 water a turn" },
        { "desert", "0", "barren", "Desert: gives nothing" },
    };

    [Theory]
    [MemberData(nameof(KnownTiles))]
    public void MarkFor_KnownTile_ShowsItsTotalAndEveryResource(string terrain, string label, string cssClass, string description)
    {
        var mark = _lens.MarkFor(new TileView(TileVisibility.Explored, new TerrainType(terrain)));

        mark.ShouldNotBeNull();
        (mark.Label, mark.Class, mark.Description).ShouldBe((label, cssClass, description));
    }

    [Fact]
    public void MarkFor_UnexploredTile_ShowsNothing()
    {
        _lens.MarkFor(new TileView(TileVisibility.Unexplored, null)).ShouldBeNull();
        _lens.MarkFor(Create(visibility: static _ => TileVisibility.Unexplored), 0).ShouldBeNull();
    }

    [Fact]
    public void MarkFor_RiverOnAnEdgeTheNeighbourOwns_AddsItsWater()
    {
        // The river runs along the east edge of (3, 2), which (3, 2) owns; its neighbour (4, 2) gets the water from the view.
        var view = Create(
            terrain: static coord => coord == At(4, 2) ? new TerrainType("desert") : Plains,
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);

        var neighbour = _lens.MarkFor(view, (2 * Width) + 4);
        neighbour!.Label.ShouldBe("1");
        neighbour.Description.ShouldBe("Desert: 1 water a turn");
        _lens.MarkFor(view.TileAt(At(4, 2)))!.Label.ShouldBe("0", "the tile alone doesn't know the river its neighbour holds");
    }

    [Fact]
    public void MarkFor_ImprovementInSight_AddsItsYield()
    {
        var view = Create(improvements: static coord => coord == At(1, 1) ? Farm : null);

        _lens.MarkFor(view, Width + 1)!.Label.ShouldBe("5");
    }
}
