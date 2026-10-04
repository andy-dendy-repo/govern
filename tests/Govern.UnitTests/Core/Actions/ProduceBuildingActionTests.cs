using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

// A P1 city at (5, 5) claims its neighbours: (5, 4), (6, 4), (4, 5), (6, 5), (5, 6) and (6, 6).
public class ProduceBuildingActionTests
{
    [Fact]
    public void Execute_NewBuilding_SwitchesProductionReservesTheSiteAndKeepsProgress()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Soldier, progress: 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(5, 4))).ShouldSucceed();

        var changed = update.State.FindCity(city)!;
        changed.ProducingBuilding.ShouldBe(TestRulesets.Campus);
        changed.ProducingBuildingSite.ShouldBe(At(5, 4));
        changed.Producing.ShouldBe(TestRulesets.Soldier);
        changed.ProductionProgress.ShouldBe(3);
        update.State.Map.TileAt(At(5, 4)).Building.ShouldBeNull("the building stands there only once it is finished");
        update.Events.ShouldHaveSingleItem().ShouldBe(new ProductionChangedEvent(city, TestRulesets.Soldier, TestRulesets.Campus));
    }

    [Fact]
    public void Execute_SameBuildingOnAnotherTile_MovesTheReservationAndKeepsWhatWasPaid()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, progress: 3, producingBuilding: TestRulesets.Campus, paid: true, buildingSite: (5, 4));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(6, 5))).ShouldSucceed();

        var moved = update.State.FindCity(city)!;
        moved.ProducingBuildingSite.ShouldBe(At(6, 5));
        moved.ProductionProgress.ShouldBe(3);
        moved.ProductionPaid.ShouldBeTrue();
    }

    [Fact]
    public void Execute_SameBuildingOnTheSameTile_ChangesNothing()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, progress: 3, producingBuilding: TestRulesets.Campus, paid: true, buildingSite: (5, 4));
        var state = builder.Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(5, 4))).ShouldSucceed();

        update.State.FindCity(city).ShouldBe(state.FindCity(city));
        update.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_SetProductionToAUnitAfterwards_StopsTheBuildingAndFreesTheSite()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, progress: 3, producingBuilding: TestRulesets.Campus, buildingSite: (5, 4));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Spearman)).ShouldSucceed();

        update.State.FindCity(city)!.ProducingBuilding.ShouldBeNull();
        update.State.FindCity(city)!.ProducingBuildingSite.ShouldBeNull();
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(3);
    }

    [Fact]
    public void Execute_BuildingAlreadyInTheCity_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithBuilding(TestRulesets.Campus, (6, 5));
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Campus]);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(5, 4)))
            .ShouldFailWith(RuleViolations.BuildingExists);
    }

    // The harbour's tile, not the city, must be next to a lake.
    [Theory]
    [InlineData(5, 3, 5, 4, true)]
    [InlineData(5, 3, 6, 5, false)]
    [InlineData(6, 4, 5, 4, true)]
    [InlineData(6, 4, 4, 5, false)]
    public void Execute_Harbour_NeedsALakeNextToItsTile(int lakeColumn, int lakeRow, int siteColumn, int siteRow, bool allowed)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (lakeColumn, lakeRow)).WithNodes(P1, "sailing");
        var city = builder.AddCity(P1, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Harbour, At(siteColumn, siteRow)));

        result.Violation.ShouldBe(allowed ? null : RuleViolations.BuildingNeedsTerrain);
    }

    public static TheoryData<string, string> SiteScenarios => new()
    {
        { "outside the territory", RuleViolations.BuildingNotInTerritory.Code },
        { "another own city's territory", RuleViolations.BuildingNotInTerritory.Code },
        { "an enemy city's territory", RuleViolations.BuildingNotInTerritory.Code },
        { "the city tile", RuleViolations.BuildingNotInTerritory.Code },
        { "off the map", RuleViolations.BuildingNotInTerritory.Code },
        { "a mountain", RuleViolations.BuildingWrongTerrain.Code },
        { "a lake", RuleViolations.BuildingWrongTerrain.Code },
        { "an improved tile", RuleViolations.BuildingSiteTaken.Code },
        { "a building's tile", RuleViolations.BuildingSiteTaken.Code },
        { "a tile an own builder improves", RuleViolations.BuildingSiteBeingImproved.Code },
    };

    [Theory]
    [MemberData(nameof(SiteScenarios))]
    public void Execute_SiteThatCantTakeABuilding_IsRefused(string scenario, string violation)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.TradePost]);
        var (column, row) = scenario switch
        {
            "outside the territory" => (8, 5),
            "another own city's territory" => (1, 0),
            "an enemy city's territory" => (10, 9),
            "the city tile" => (5, 5),
            "off the map" => (-1, 20),
            _ => (5, 4),
        };

        switch (scenario)
        {
            case "a mountain":
                builder.WithTerrain(TestRulesets.Mountains, (5, 4));
                break;
            case "a lake":
                builder.WithTerrain(TestRulesets.Lake, (5, 4));
                break;
            case "an improved tile":
                builder.WithImprovement(TestRulesets.Farm, (5, 4));
                break;
            case "a building's tile":
                builder.WithBuilding(TestRulesets.TradePost, (5, 4));
                break;
            case "a tile an own builder improves":
                builder.AddUnit(P1, TestRulesets.Builder, 5, 4, job: UnitJob.ForImprovement(TestRulesets.Farm, 1));
                break;
        }

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(column, row)))
            .Violation!.Code.ShouldBe(violation);
    }

    [Fact]
    public void Execute_SiteWithAUnitThatIsNotImprovingIt_IsAllowed()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);
        builder.AddUnit(P1, TestRulesets.Soldier, 5, 4);
        builder.AddUnit(P1, TestRulesets.Soldier, 6, 5, job: UnitJob.ForBridge(At(7, 5), 1));

        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(5, 4))).ShouldSucceed();
        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(6, 5))).ShouldSucceed();
    }

    [Theory]
    [InlineData("bank")]
    [InlineData("")]
    public void Execute_UnknownBuilding_IsRefused(string building)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, new BuildingType(building), At(5, 4)))
            .ShouldFailWith(RuleViolations.UnknownBuilding);
    }

    [Fact]
    public void Execute_EnemyOrMissingCity_ReturnsNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddCity(P2, 5, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(enemy, TestRulesets.Campus, At(5, 4))).ShouldFailWith(RuleViolations.CityNotFound);
        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(new CityId(999), TestRulesets.Campus, At(5, 4))).ShouldFailWith(RuleViolations.CityNotFound);
    }
}
