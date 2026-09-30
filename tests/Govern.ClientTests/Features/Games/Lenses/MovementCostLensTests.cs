using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Services;
using Govern.Client.Helpers;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Lenses;

public class MovementCostLensTests
{
    private readonly MovementCostLens _lens = new(new RulesetService());

    public static TheoryData<string, TileVisibility, string, string> KnownTiles => new()
    {
        { "plains", TileVisibility.Visible, "1", "cheap" },
        { "forest", TileVisibility.Visible, "2", "costly" },
        { "hills", TileVisibility.Explored, "2", "costly" },
        { "mountains", TileVisibility.Visible, "✕", "blocked" },
        { "lake", TileVisibility.Explored, "✕", "blocked" },
    };

    [Theory]
    [MemberData(nameof(KnownTiles))]
    public void MarkFor_KnownTile_ShowsItsCostOrThatItIsBlocked(string terrain, TileVisibility visibility, string label, string cssClass)
    {
        var mark = _lens.MarkFor(new TileView(visibility, new TerrainType(terrain)));

        mark.ShouldNotBeNull();
        mark.Label.ShouldBe(label);
        mark.Class.ShouldBe(cssClass);
        mark.Description.ShouldStartWith(Ruleset.TerrainName(new TerrainType(terrain)));
    }

    [Fact]
    public void MarkFor_UnexploredTile_ShowsNothing()
    {
        _lens.MarkFor(new TileView(TileVisibility.Unexplored, null)).ShouldBeNull();
    }

    [Fact]
    public void Lens_Button_UsesTheMovementIcon()
    {
        (_lens.Id, _lens.Name, _lens.Icon).ShouldBe(("movement-cost", "Movement cost", Icons.Movement));
    }
}
