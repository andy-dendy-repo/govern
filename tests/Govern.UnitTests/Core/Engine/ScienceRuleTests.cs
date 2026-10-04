using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

public class ScienceRuleTests
{
    [Fact]
    public void Balance_IncomeBelowTheNodes_DisablesTheNewestThenEnablesItWhenTheIncomeIsBack()
    {
        // The capital and the city make 1 science each; the campus makes nothing at 0 health but recovers when its gold is paid.
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery", "sailing", "education").WithBuilding(TestRulesets.Campus, (6, 5));
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Gold, 1)], buildings: [TestRulesets.Campus]);
        builder.WithCity(city, c => c with { BuildingDamage = [new BuildingDamage(TestRulesets.Campus, 100)] });

        var first = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetUpkeepPriorityCommand(city, UpkeepPriority.UnitsFirst)).ShouldSucceed();

        first.State.FindPlayer(P1)!.NodeList.ShouldBe([new OwnedNode("archery"), new OwnedNode("sailing"), new OwnedNode("education", Disabled: true)]);
        first.Events.ShouldContain(new ScienceNodeChangedEvent(P1, "education", ScienceNodeChange.Disabled));
        GameEngine.Execute(TestRulesets.Default, first.State, P1, new ProduceBuildingCommand(city, TestRulesets.Harbour, At(5, 4)))
            .ShouldFailWith(RuleViolations.BuildingNeedsTerrain);

        var second = GameEngine.Execute(TestRulesets.Default, first.State, P1, new EndTurnCommand()).ShouldSucceed();

        second.State.FindPlayer(P1)!.NodeList.ShouldAllBe(static node => !node.Disabled);
        second.Events.ShouldContain(new ScienceNodeChangedEvent(P1, "education", ScienceNodeChange.Enabled));
    }

    [Fact]
    public void Balance_LostIncome_DisablesNewestFirstUntilItFits()
    {
        var state = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery", "sailing", "trade").Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindPlayer(P1)!.NodeList.ShouldBe([new OwnedNode("archery"), new OwnedNode("sailing", Disabled: true), new OwnedNode("trade", Disabled: true)]);
        update.Events.OfType<ScienceNodeChangedEvent>().Select(static changed => changed.Node).ShouldBe(["trade", "sailing"]);
    }

    [Fact]
    public void Balance_DisabledNode_LocksWhatItUnlocksButKeepsWhatExists()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, new OwnedNode("archery", Disabled: true));
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Archer, progress: 4, paid: true);
        var archer = builder.AddUnit(P1, TestRulesets.Archer, 6, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Archer)).ShouldFailWith(RuleViolations.Locked);
        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().UnitType.ShouldBe(TestRulesets.Archer, "an item already started is finished");
        update.State.FindCity(city)!.Producing.ShouldBe(TestRulesets.Soldier, "then the city goes back to its first unit");
        update.State.FindUnit(archer).ShouldNotBeNull();
    }

    [Fact]
    public void Production_NewerUnitUnlocked_ReplacesTheOldOne()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "bronze_working");
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Soldier, progress: 3, paid: true);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Soldier)).ShouldFailWith(RuleViolations.Replaced);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Swordsman)).ShouldSucceed();
        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().UnitType.ShouldBe(TestRulesets.Soldier, "the soldier already started is finished");
        update.State.FindCity(city)!.Producing.ShouldBe(TestRulesets.Swordsman);
    }

    [Theory]
    [InlineData("bronze_working", false, "soldier", "swordsman")]
    [InlineData("archery", true, "archer", "soldier")]
    public void ProduceBuilding_Finished_GoesBackToTheNewestUnitThePlayerMayProduce(string node, bool disabled, string unit, string expected)
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, new OwnedNode(node, disabled));
        var city = builder.AddCity(P1, 5, 5, new Govern.Core.Units.UnitType(unit), progress: 7, producingBuilding: TestRulesets.Campus, paid: true, buildingSite: (5, 4));

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new EndTurnCommand()).ShouldSucceed();

        update.Events.OfType<BuildingCompletedEvent>().ShouldHaveSingleItem();
        update.State.FindCity(city)!.Producing.ShouldBe(new Govern.Core.Units.UnitType(expected));
    }

    [Fact]
    public void Balance_OldestDisabledNodeDoesNotFit_NewerOnesWaitBehindIt()
    {
        // One city makes 1 science: education (2) can't come back, and archery (1) is not let past it.
        var state = new GameStateBuilder().WithCapitals()
            .WithNodes(P1, new OwnedNode("education", Disabled: true), new OwnedNode("archery", Disabled: true)).Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.FindPlayer(P1)!.NodeList.ShouldAllBe(static owned => owned.Disabled);
        update.Events.OfType<ScienceNodeChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Production_LockedUnit_IsRefusedUntilItsNodeIsOwned()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);
        var state = builder.Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Swordsman)).ShouldFailWith(RuleViolations.Locked);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Horseman)).ShouldFailWith(RuleViolations.Locked);
        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.TradePost, At(5, 4))).ShouldFailWith(RuleViolations.Locked);
        GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.University, At(5, 4))).ShouldFailWith(RuleViolations.BuildingIsUpgrade);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Spearman)).ShouldSucceed();
    }

    [Fact]
    public void FoundCity_StartsTheNewestVersionOfTheFirstUnit()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "bronze_working");
        var settler = builder.AddUnit(P1, TestRulesets.Settler, 6, 6);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new FoundCityCommand(settler, "Elmstead")).ShouldSucceed();

        update.State.Cities.Single(city => city.Name == "Elmstead").Producing.ShouldBe(TestRulesets.Swordsman);
    }

    [Fact]
    public void ProduceBuilding_CityHasTheUpgrade_CountsAsHavingTheBuilding()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "education").WithBuilding(TestRulesets.University, (6, 5));
        var city = builder.AddCity(P1, 5, 5, buildings: [TestRulesets.University]);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ProduceBuildingCommand(city, TestRulesets.Campus, At(5, 4)))
            .ShouldFailWith(RuleViolations.BuildingExists);
    }

    [Fact]
    public void View_Science_IsTheViewersOwnOnly()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "archery").WithBuilding(TestRulesets.Campus, (6, 5));
        builder.AddCity(P1, 5, 5, buildings: [TestRulesets.Campus]);
        var state = builder.Build();

        var own = GameEngine.ViewFor(TestRulesets.Default, state, P1).Science!;
        var other = GameEngine.ViewFor(TestRulesets.Default, state, P2).Science!;

        own.Income.ShouldBe(4);
        own.NodeList.ShouldBe([new OwnedNode("archery")]);
        other.Income.ShouldBe(1);
        other.NodeList.ShouldBeEmpty();
    }
}
