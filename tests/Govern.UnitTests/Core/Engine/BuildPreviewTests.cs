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
    // The tested unit stands at (5, 5); a P1 city at (6, 4) puts it in a territory unless the scenario says otherwise.
    public static TheoryData<string> UnitScenarios => new(
        "builder on plains in territory",
        "builder on forest in territory",
        "builder on hills already improved",
        "builder on a building's tile",
        "builder on a tile reserved for a building",
        "builder outside every territory",
        "builder in an enemy territory",
        "builder without movement",
        "builder already building a farm",
        "builder by rivers and a bridge",
        "soldier by rivers",
        "soldier by a river into an enemy territory",
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

    // The tested city stands at (5, 5) and claims its neighbours.
    public static TheoryData<string> CityScenarios => new(
        "plain city",
        "city by a lake",
        "lake beside the territory",
        "city with a campus",
        "city producing a campus",
        "territory improved, built on and being improved",
        "city surrounded by mountains",
        "territory beyond the city's sight with a lake just outside it",
        "enemy city",
        "not your turn");

    private static readonly BuildingType[] Buildings =
        [TestRulesets.ArmyCamp, TestRulesets.Campus, TestRulesets.TradePost, TestRulesets.CultureCentre, TestRulesets.Harbour, new("bank")];

    [Theory]
    [MemberData(nameof(CityScenarios))]
    public void BuildingSites_View_MatchTheCommandOnEveryNearbyTile(string scenario)
    {
        var (state, city) = BuildCity(scenario);
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        foreach (var building in Buildings)
        {
            var sites = GameEngine.BuildingSites(TestRulesets.Default, view, city, building);
            foreach (var site in At(5, 5).WithinRadius(4))
            {
                var accepted = GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, building, site)).IsSuccess;
                sites.Contains(site).ShouldBe(accepted, $"{building.Id} on {site}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(CityScenarios))]
    public void CheckProduceBuilding_View_MatchesTheCommand(string scenario)
    {
        var (state, city) = BuildCity(scenario);
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        foreach (var building in Buildings)
        {
            var violations = At(5, 5).WithinRadius(4)
                .Select(site => GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, building, site)).Violation)
                .ToList();
            var check = GameEngine.CheckProduceBuilding(TestRulesets.Default, view, city, building);

            if (violations.Contains(null))
            {
                check.ShouldBeNull(building.Id);
            }
            else
            {
                violations.ShouldContain(check, building.Id);
            }
        }
    }

    [Fact]
    public void CheckProduceBuilding_NoTileNextToALake_SaysTheHarbourNeedsOne()
    {
        var (state, city) = BuildCity("plain city");

        GameEngine.CheckProduceBuilding(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city, TestRulesets.Harbour)
            .ShouldBe(RuleViolations.BuildingNeedsTerrain);
    }

    [Fact]
    public void BuildingSites_TerritoryBeyondSight_OffersAHarbourByTheLakeOnlyTheExploredRingShows()
    {
        var (state, city) = BuildCity("territory beyond the city's sight with a lake just outside it");

        GameEngine.BuildingSites(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city, TestRulesets.Harbour)
            .ShouldBe([At(8, 5)]);
    }

    [Fact]
    public void CheckProduceBuilding_NoTileFree_GivesTheReasonOfTheTileThatCameClosest()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithTerrain(TestRulesets.Mountains, (6, 5), (4, 5), (5, 4))
            .WithImprovement(TestRulesets.Farm, (6, 4), (5, 6));
        var city = builder.AddCity(P1, 5, 5);
        builder.AddUnit(P1, TestRulesets.Builder, 6, 6, job: UnitJob.ForImprovement(TestRulesets.Farm, 1));
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        GameEngine.CheckProduceBuilding(TestRulesets.Default, view, city, TestRulesets.Campus).ShouldBe(RuleViolations.BuildingSiteBeingImproved);
    }

    [Fact]
    public void BuildingSites_FreeTerritory_ListsItsTilesInRowOrderWithoutTheCityTile()
    {
        var (state, city) = BuildCity("plain city");

        GameEngine.BuildingSites(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city, TestRulesets.Campus)
            .ShouldBe([At(5, 4), At(6, 4), At(4, 5), At(6, 5), At(5, 6), At(6, 6)]);
    }

    private static (GameState State, CityId City) BuildCity(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = scenario switch
        {
            "city by a lake" => builder.WithTerrain(TestRulesets.Lake, (5, 6)).AddCity(P1, 5, 5),
            "lake beside the territory" => builder.WithTerrain(TestRulesets.Lake, (5, 3)).AddCity(P1, 5, 5),
            "city with a campus" => builder.WithBuilding(TestRulesets.Campus, (6, 5)).AddCity(P1, 5, 5, buildings: [TestRulesets.Campus]),
            "city producing a campus" => builder.AddCity(P1, 5, 5, producingBuilding: TestRulesets.Campus, buildingSite: (5, 4)),
            "territory improved, built on and being improved" => builder
                .WithImprovement(TestRulesets.Farm, (5, 4), (6, 4))
                .WithBuilding(TestRulesets.TradePost, (4, 5))
                .AddCity(P1, 5, 5, buildings: [TestRulesets.TradePost]),
            "city surrounded by mountains" => builder.WithTerrain(TestRulesets.Mountains, (6, 5), (4, 5), (5, 4), (6, 4), (5, 6), (6, 6)).AddCity(P1, 5, 5),
            "enemy city" => builder.AddCity(P2, 5, 5),
            _ => builder.AddCity(P1, 5, 5),
        };

        // Tiles 3 away, past the city's sight, with a lake 4 away that only the explored ring around the territory shows.
        if (scenario == "territory beyond the city's sight with a lake just outside it")
        {
            builder.WithTerritory(city, (7, 5), (8, 5), (3, 5), (2, 5)).WithTerrain(TestRulesets.Lake, (9, 5));
        }

        if (scenario == "territory improved, built on and being improved")
        {
            builder.AddUnit(P1, TestRulesets.Builder, 6, 5, job: UnitJob.ForImprovement(TestRulesets.Farm, 1));
            builder.AddUnit(P2, TestRulesets.Soldier, 5, 6);
        }

        return ((scenario == "not your turn" ? builder.WithCurrentPlayer(P2) : builder).Build(), city);
    }

    [Fact]
    public void Previews_NullArguments_Throw()
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, new GameStateBuilder().WithCapitals().Build(), P1);

        Should.Throw<ArgumentNullException>(() => GameEngine.CheckBuildImprovement(null!, view, new UnitId(1), TestRulesets.Farm));
        Should.Throw<ArgumentNullException>(() => GameEngine.BridgeTargets(TestRulesets.Default, null!, new UnitId(1)));
        Should.Throw<ArgumentNullException>(() => GameEngine.CheckProduceBuilding(TestRulesets.Default, null!, new CityId(1), TestRulesets.Campus));
        Should.Throw<ArgumentNullException>(() => GameEngine.BuildingSites(null!, view, new CityId(1), TestRulesets.Campus));
    }

    private static (GameState State, UnitId Unit) BuildUnit(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        if (scenario is not ("builder outside every territory" or "builder in an enemy territory" or "soldier by a river into an enemy territory"
            or "builder on a tile reserved for a building"))
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
            "builder on forest in territory" => builder.WithTerrain(TestRulesets.Forest, (5, 5)),
            "builder on hills already improved" => builder.WithTerrain(TestRulesets.Hills, (5, 5)).WithImprovement(TestRulesets.Mine, (5, 5)),
            "builder on a building's tile" => builder.WithBuilding(TestRulesets.Campus, (5, 5)),
            "builder on a tile reserved for a building" => builder.AddCity(P1, 6, 4, producingBuilding: TestRulesets.Campus, buildingSite: (5, 5)) is var _ ? builder : builder,
            "builder outside every territory" => builder.AddCity(P1, 9, 5) is var _ ? builder : builder,
            "builder in an enemy territory" => builder.AddCity(P2, 6, 4) is var _ ? builder : builder,
            // A stream east, a deep river north-west, a river west, a bridged river south-east, and a lake south-west.
            "builder by rivers and a bridge" or "soldier by rivers" or "settler by a river" or "archer by a river" or "soldier already bridging" => builder
                .WithRiver(TestRulesets.Stream, ((5, 5), (6, 5)))
                .WithRiver(TestRulesets.DeepRiver, ((5, 5), (5, 4)))
                .WithRiver(TestRulesets.River, ((5, 5), (4, 5)), ((5, 5), (6, 6)))
                .WithBridge(((5, 5), (6, 6)))
                .WithTerrain(TestRulesets.Lake, (5, 6)),
            // The enemy city two tiles west covers the far bank (4, 5).
            "soldier by a river into an enemy territory" => builder
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
