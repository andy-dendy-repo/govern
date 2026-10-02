using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Core.Map;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class TerrainReliefsTests
{
    [Fact]
    public void Known_EveryRulesetTerrain_HasARelief()
    {
        var missing = Ruleset.Terrains.Select(static terrain => terrain.Id.Id).Except(TerrainReliefs.Known).ToList();

        missing.ShouldBeEmpty("every ruleset terrain needs its 3D relief in Features/Games/Helpers/TerrainReliefs");
    }

    [Fact]
    public void For_UnknownTerrain_IsTheFlatDefault()
    {
        TerrainReliefs.For(new TerrainType("swamp")).ShouldBe(TerrainReliefs.Default);
        TerrainReliefs.Default.Shape.ShouldBe(TileShape.Flat);
    }
}
