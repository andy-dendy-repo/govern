using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Core.Economy;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class FeatureModelsTests
{
    [Fact]
    public void KnownImprovements_EveryRulesetImprovement_HasAModel()
    {
        var missing = Ruleset.Improvements.Select(static improvement => improvement.Id.Id).Except(FeatureModels.KnownImprovements).ToList();

        missing.ShouldBeEmpty("every ruleset improvement needs its 3D model in Features/Games/Helpers/FeatureModels");
    }

    [Fact]
    public void KnownBuildings_EveryRulesetBuilding_HasAModel()
    {
        var missing = Ruleset.Buildings.Select(static building => building.Id.Id).Except(FeatureModels.KnownBuildings).ToList();

        missing.ShouldBeEmpty("every ruleset building needs its 3D model in Features/Games/Helpers/FeatureModels");
    }

    [Fact]
    public void For_KnownIds_TakeTheirModel()
    {
        FeatureModels.For(Farm).ShouldBe(FeatureModel.Field);
        FeatureModels.For(Campus).ShouldBe(FeatureModel.Dome);
        FeatureModels.For(Harbour).ShouldBe(FeatureModel.Pier);
    }

    [Fact]
    public void For_UnknownIds_TakeTheDefaults()
    {
        FeatureModels.For(new ImprovementType("quarry")).ShouldBe(FeatureModels.DefaultImprovement);
        FeatureModels.For(new BuildingType("temple")).ShouldBe(FeatureModels.DefaultBuilding);
    }
}
