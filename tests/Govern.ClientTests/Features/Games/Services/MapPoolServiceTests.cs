using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Games.Services;

public class MapPoolServiceTests : ClientTestContext
{
    private MapPoolService Pool => Services.GetRequiredService<MapPoolService>();

    [Fact]
    public void Maps_AreTheEmbeddedPool()
    {
        var basin = Pool.Maps.ShouldHaveSingleItem();
        basin.Key.ShouldBe("basin");
        basin.Map.Width.ShouldBe(32);
        basin.Map.Height.ShouldBe(20);
    }

    [Fact]
    public void OptionsFor_ListRandomThenEachMapForTheSeatsThenGenerated()
    {
        Pool.OptionsFor(2).ShouldBe(
        [
            new MapOption("random", "Random map from the pool"),
            new MapOption("basin", "The Basin"),
            new MapOption("generated", "Generated map"),
        ]);
        Pool.OptionsFor(2).ShouldBeSameAs(Pool.OptionsFor(2));
    }

    [Fact]
    public void OptionsFor_SeatsNoPoolMapIsFor_LeavesOutTheTwoPlayerMaps()
    {
        Pool.OptionsFor(3).Select(static option => option.Value).ShouldBe(["random", "generated"]);
    }

    [Theory]
    [InlineData("basin", "The Basin")]
    [InlineData("random", "Random map from the pool")]
    [InlineData("generated", "Generated map")]
    [InlineData(null, "Generated map")]
    [InlineData("unknown_map", "unknown_map")]
    public void NameOf_GivesWhatPlayersSee(string? map, string name)
    {
        Pool.NameOf(map).ShouldBe(name);
    }
}
