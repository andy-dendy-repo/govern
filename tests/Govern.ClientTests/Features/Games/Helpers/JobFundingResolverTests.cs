using Govern.Client.Features.Games.Helpers;
using Govern.Core.Cities;
using Govern.Core.Units;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class JobFundingResolverTests
{
    [Fact]
    public void FeedingCities_TwoJobsOnOneCitysGround_FeedsOnlyTheOldest()
    {
        var older = Unit(1, You, BuilderType, At(3, 2)) with { Job = new UnitJob(0, 2, Improvement: Farm) };
        var newer = Unit(2, You, BuilderType, At(1, 2)) with { Job = new UnitJob(0, 1, Improvement: Farm) };
        var view = Create([older, newer], [City(7, You, At(2, 2))]);

        var fed = JobFundingResolver.FeedingCities(Ruleset, view);

        fed.Keys.ShouldBe([new UnitId(2)]);
        fed[new UnitId(2)].Id.ShouldBe(new CityId(7));
    }

    [Fact]
    public void FeedingCities_JobOffEveryCitysGround_IsNotFed()
    {
        var builder = Unit(1, You, BuilderType, At(6, 4)) with { Job = new UnitJob(0, 1, Improvement: Farm) };

        JobFundingResolver.FeedingCities(Ruleset, Create([builder], [City(7, You, At(2, 2))])).ShouldBeEmpty();
    }
}
