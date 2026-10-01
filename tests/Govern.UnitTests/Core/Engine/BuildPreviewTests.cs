using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// The building previews must give exactly the answer Execute gives on the full state: every improvement for the unit's tile,
// every tile near the unit as a bridge target, and every building for a city.
public class BuildPreviewTests
{
    // The tested unit stands at (5, 5); a P1 city at (6, 4) puts it in a work area unless the scenario says otherwise.
    public static TheoryData<string> UnitScenarios => new(
        "builder on plains in work area",
        "builder on forest in work area",
        "builder on hills already improved",
        "builder outside every work area",
        "builder in an enemy work area",
        "builder without movement",
        "builder already building a farm",
        "builder by rivers and a bridge",
        "soldier by rivers",
        "soldier by a river into an enemy work area",
        "soldier already bridging",
        "river into a lake",
        "settler by a river",
        "archer by a river",
        "not your turn",
        "enemy unit id",
        "game finished");

    private static readonly ImprovementType[] Improvements = [TestRulesets.Farm, TestRulesets.LumberCamp, TestRulesets.Mine, new("plantation")];

    [Theory]
    [MemberData(nameof(UnitScenarios))]
    public void CheckBuildImprovement_View_MatchesTheCommand(string scenario)
    {
        var (state, unit) = BuildUnit(scenario);
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        foreach (var improvement in Improvements)
        {
            GameEngine.CheckBuildImprovement(TestRulesets.Default, view, unit, improvement)
                .ShouldBe(GameEngine.Execute(TestRulesets.Default, state, P1, new BuildImprovementCommand(unit, improvement)).Violation, improvement.Id);
        }
    }

    [Theory]
    [MemberData(nameof(UnitScenarios))]
    public void BridgeTargets_View_MatchesTheCommand(string scenario)
    {
        var (state, unit) = BuildUnit(scenario);

        var targets = GameEngine.BridgeTargets(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit);

        foreach (var target in At(5, 5).WithinRadius(2))
        {
            var accepted = GameEngine.Execute(TestRulesets.Default, state, P1, new BuildBridgeCommand(unit, target)).IsSuccess;
            targets.Contains(target).ShouldBe(accepted, $"bridge to {target}");
        }
    }

    [Fact]
    public void BridgeTargets_RiversAround_ListsTheUnbridgedCrossableSidesInRowOrder()
    {
        var (state, unit) = BuildUnit("builder by rivers and a bridge");

        GameEngine.BridgeTargets(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit).ShouldBe([At(5, 4), At(4, 5), At(6, 5)]);
    }

    public static TheoryData<string> CityScenarios => new(
        "plain city",
        "city by a lake",
        "city with a campus",
        "city producing a campus",
        "enemy city",
        "not your turn");

    private static readonly BuildingType[] Buildings =
        [TestRulesets.ArmyCamp, TestRulesets.Campus, TestRulesets.TradePost, TestRulesets.CultureCentre, TestRulesets.Harbour, new("bank")];

    [Theory]
    [MemberData(nameof(CityScenarios))]
    public void CheckProduceBuilding_View_MatchesTheCommand(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = scenario switch
        {
            "city by a lake" => builder.WithTerrain(TestRulesets.Lake, (5, 6)).AddCity(P1, 5, 5),
            "city with a campus" => builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Campus]),
            "city producing a campus" => builder.AddCity(P1, 5, 5, producingBuilding: TestRulesets.Campus),
            "enemy city" => builder.AddCity(P2, 5, 5),
            _ => builder.AddCity(P1, 5, 5),
        };
        var state = (scenario == "not your turn" ? builder.WithCurrentPlayer(P2) : builder).Build();
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        foreach (var building in Buildings)
        {
            GameEngine.CheckProduceBuilding(TestRulesets.Default, view, city, building)
                .ShouldBe(GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, building)).Violation, building.Id);
        }
    }

    [Fact]
    public void Previews_NullArguments_Throw()
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, new GameStateBuilder().WithCapitals().Build(), P1);

        Should.Throw<ArgumentNullException>(() => GameEngine.CheckBuildImprovement(null!, view, new UnitId(1), TestRulesets.Farm));
        Should.Throw<ArgumentNullException>(() => GameEngine.BridgeTargets(TestRulesets.Default, null!, new UnitId(1)));
        Should.Throw<ArgumentNullException>(() => GameEngine.CheckProduceBuilding(TestRulesets.Default, null!, new CityId(1), TestRulesets.Campus));
    }

    private static (GameState State, UnitId Unit) BuildUnit(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        if (scenario is not ("builder outside every work area" or "builder in an enemy work area" or "soldier by a river into an enemy work area"))
        {
            builder.AddCity(P1, 6, 4);
        }

        UnitType type = scenario switch
        {
            _ when scenario.StartsWith("soldier", StringComparison.Ordinal) || scenario == "river into a lake" => TestRulesets.Soldier,
            "settler by a river" => TestRulesets.Settler,
            "archer by a river" => TestRulesets.Archer,
            _ => TestRulesets.Builder,
        };
        UnitJob? job = scenario switch
        {
            "builder already building a farm" => new UnitJob(2, 1, Improvement: TestRulesets.Farm),
            "soldier already bridging" => new UnitJob(2, 1, BridgeTo: At(4, 5)),
            _ => null,
        };
        var owner = scenario == "enemy unit id" ? P2 : P1;
        var unit = builder.AddUnit(owner, type, 5, 5, movementLeft: scenario == "builder without movement" ? 0 : null, job: job);

        _ = scenario switch
        {
            "builder on forest in work area" => builder.WithTerrain(TestRulesets.Forest, (5, 5)),
            "builder on hills already improved" => builder.WithTerrain(TestRulesets.Hills, (5, 5)).WithImprovement(TestRulesets.Mine, (5, 5)),
            "builder outside every work area" => builder.AddCity(P1, 9, 5) is var _ ? builder : builder,
            "builder in an enemy work area" => builder.AddCity(P2, 6, 4) is var _ ? builder : builder,
            // A stream east, a deep river north-west, a river west, a bridged river south-east, and a lake south-west.
            "builder by rivers and a bridge" or "soldier by rivers" or "settler by a river" or "archer by a river" or "soldier already bridging" => builder
                .WithRiver(TestRulesets.Stream, ((5, 5), (6, 5)))
                .WithRiver(TestRulesets.DeepRiver, ((5, 5), (5, 4)))
                .WithRiver(TestRulesets.River, ((5, 5), (4, 5)), ((5, 5), (6, 6)))
                .WithBridge(((5, 5), (6, 6)))
                .WithTerrain(TestRulesets.Lake, (5, 6)),
            // The enemy city two tiles west covers the far bank (4, 5).
            "soldier by a river into an enemy work area" => builder
                .WithRiver(TestRulesets.River, ((5, 5), (4, 5)), ((5, 5), (6, 5)))
                .AddCity(P2, 3, 5) is var _ ? builder : builder,
            "river into a lake" => builder.WithRiver(TestRulesets.River, ((5, 5), (6, 5))).WithTerrain(TestRulesets.Lake, (6, 5)),
            _ => builder,
        };

        if (scenario == "enemy unit id")
        {
            builder.AddUnit(P1, TestRulesets.Builder, 3, 5);
        }

        var state = (scenario == "not your turn" ? builder.WithCurrentPlayer(P2) : builder).Build();
        state = scenario == "game finished" ? state with { Status = GameStatus.Finished, Winner = P1 } : state;
        return (state, unit);
    }
}
