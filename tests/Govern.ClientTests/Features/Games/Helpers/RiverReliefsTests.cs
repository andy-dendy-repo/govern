using Govern.Client.Features.Games.Helpers;
using Govern.Core.Map;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class RiverReliefsTests
{
    [Fact]
    public void Known_EveryRulesetRiver_HasARelief()
    {
        var missing = Ruleset.Rivers.Select(static river => river.Id.Id).Except(RiverReliefs.Known).ToList();

        missing.ShouldBeEmpty("every ruleset river needs its 3D width in Features/Games/Helpers/RiverReliefs");
    }

    [Fact]
    public void For_DeeperKinds_AreWider()
    {
        RiverReliefs.For(StreamRiver).Width.ShouldBeLessThan(RiverReliefs.For(River).Width);
        RiverReliefs.For(River).Width.ShouldBeLessThan(RiverReliefs.For(DeepRiver).Width);
        RiverReliefs.For(DeepRiver).Deep.ShouldBeTrue();
    }

    [Fact]
    public void For_UnknownRiver_IsTheDefault() => RiverReliefs.For(new RiverType("canal")).ShouldBe(RiverReliefs.Default);
}
