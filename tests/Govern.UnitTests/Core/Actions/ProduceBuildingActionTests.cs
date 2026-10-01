using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class ProduceBuildingActionTests
{
    [Fact]
    public void Execute_NewBuilding_SwitchesProductionAndKeepsProgress()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Soldier, progress: 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus)).ShouldSucceed();

        var changed = update.State.FindCity(city)!;
        changed.ProducingBuilding.ShouldBe(TestRulesets.Campus);
        changed.Producing.ShouldBe(TestRulesets.Soldier);
        changed.ProductionProgress.ShouldBe(3);
        update.Events.ShouldHaveSingleItem().ShouldBe(new ProductionChangedEvent(city, TestRulesets.Soldier, TestRulesets.Campus));
    }

    [Fact]
    public void Execute_SetProductionToAUnitAfterwards_StopsTheBuilding()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, progress: 3, producingBuilding: TestRulesets.Campus);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Archer)).ShouldSucceed();

        update.State.FindCity(city)!.ProducingBuilding.ShouldBeNull();
        update.State.FindCity(city)!.ProductionProgress.ShouldBe(3);
    }

    [Fact]
    public void Execute_BuildingAlreadyInTheCity_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Campus]);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus))
            .ShouldFailWith(RuleViolations.BuildingExists);
    }

    [Fact]
    public void Execute_HarbourAwayFromALake_IsRefused()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (5, 3));
        var city = builder.AddCity(P1, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Harbour))
            .ShouldFailWith(RuleViolations.BuildingNeedsTerrain);
    }

    [Fact]
    public void Execute_HarbourNextToALake_IsAllowed()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (6, 4));
        var city = builder.AddCity(P1, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Harbour)).ShouldSucceed();
    }

    [Theory]
    [InlineData("bank")]
    [InlineData("")]
    public void Execute_UnknownBuilding_IsRefused(string building)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, new BuildingType(building)))
            .ShouldFailWith(RuleViolations.UnknownBuilding);
    }

    [Fact]
    public void Execute_EnemyOrMissingCity_ReturnsNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddCity(P2, 5, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(enemy, TestRulesets.Campus)).ShouldFailWith(RuleViolations.CityNotFound);
        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(new CityId(999), TestRulesets.Campus)).ShouldFailWith(RuleViolations.CityNotFound);
    }
}
