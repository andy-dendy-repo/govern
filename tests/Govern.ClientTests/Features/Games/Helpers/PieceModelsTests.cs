using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Core.Units;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class PieceModelsTests
{
    [Fact]
    public void KnownUnits_EveryRulesetUnit_HasAModel()
    {
        var missing = Ruleset.Units.Select(static unit => unit.Id.Id).Except(PieceModels.KnownUnits).ToList();

        missing.ShouldBeEmpty("every ruleset unit needs its 3D model in Features/Games/Helpers/PieceModels");
    }

    [Fact]
    public void For_KnownAndUnknownUnits_TakeTheirModelOrTheDefault()
    {
        PieceModels.For(HorsemanType).ShouldBe(PieceModel.Horseman);
        PieceModels.For(new UnitType("catapult")).ShouldBe(PieceModels.DefaultUnit);
    }
}
