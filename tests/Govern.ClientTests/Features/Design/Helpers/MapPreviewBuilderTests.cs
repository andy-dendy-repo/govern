using Govern.Client.Features.Design.Helpers;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Design.Helpers;

public class MapPreviewBuilderTests
{
    private static readonly PredefinedMap Map = PredefinedMap.ParseValid(Ruleset, """
        {
          "key": "tiny", "name": "Tiny", "width": 8, "height": 8,
          "legend": { ".": "plains", "~": "lake" },
          "rows": ["........", "........", "...~....", "........", "........", "....~...", "........", "........"],
          "rivers": [ { "tile": [2, 4], "side": "east", "river": "stream" }, { "tile": [4, 3], "side": "east", "river": "stream" } ],
          "firstSpawns": [[1, 2], [1, 5]], "secondSpawns": [[6, 5], [6, 2]]
        }
        """);

    [Fact]
    public void Build_ShowsEveryTileInSightWithItsTerrainAndRivers()
    {
        var view = MapPreviewBuilder.Build(Ruleset, Map, version: 3);

        view.Version.ShouldBe(3);
        view.Width.ShouldBe(8);
        view.Tiles.ShouldAllBe(static tile => tile.Visibility == TileVisibility.Visible);
        view.TileAt(HexCoord.FromOffset(3, 2)).Terrain.ShouldBe(new TerrainType("lake"));
        view.RiverBetween(HexCoord.FromOffset(2, 4), HexCoord.FromOffset(3, 4)).ShouldBe(new RiverType("stream"));
    }

    [Fact]
    public void Build_PutsAUnitOnEverySpawnCandidateOfBothSides()
    {
        var view = MapPreviewBuilder.Build(Ruleset, Map, version: 1);

        view.Units.Where(static unit => unit.Owner == MapPreviewBuilder.You).Select(static unit => unit.Position).ShouldBe(Map.FirstSpawns);
        view.Units.Where(static unit => unit.Owner == MapPreviewBuilder.Them).Select(static unit => unit.Position).ShouldBe(Map.SecondSpawns);
        view.Units.Select(static unit => unit.Id).Distinct().Count().ShouldBe(4);
        view.Units.ShouldAllBe(unit => unit.Type == Ruleset.Setup.StartingUnits[0]);
    }
}
