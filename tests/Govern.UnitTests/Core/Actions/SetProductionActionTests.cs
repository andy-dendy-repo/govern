using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class SetProductionActionTests
{
    [Fact]
    public void Execute_OwnCity_ChangesProductionAndKeepsProgress()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5, TestRulesets.Soldier, progress: 2);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Settler)).ShouldSucceed();

        var changed = update.State.FindCity(city)!;
        changed.Producing.ShouldBe(TestRulesets.Settler);
        changed.ProductionProgress.ShouldBe(2);
        update.Events.ShouldHaveSingleItem().ShouldBe(new ProductionChangedEvent(city, TestRulesets.Settler));
    }

    [Fact]
    public void Execute_UnknownCity_ReturnsNotFound()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(new CityId(999), TestRulesets.Settler));

        result.ShouldFailWith(RuleViolations.CityNotFound);
    }

    [Fact]
    public void Execute_EnemyCity_ReturnsNotFoundLikeAMissingId()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, TestRulesets.Settler));

        result.ShouldFailWith(RuleViolations.CityNotFound);
    }

    [Theory]
    [InlineData("dragoon")]
    [InlineData("")]
    [InlineData("Settler")]
    public void Execute_UnitTypeNotInTheRuleset_ReturnsUnknownUnitType(string unitType)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);

        var result = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new SetProductionCommand(city, new UnitType(unitType)));

        result.ShouldFailWith(RuleViolations.UnknownUnitType);
    }

    [Fact]
    public void Execute_UnitAddedByTheRuleset_IsAccepted()
    {
        var slinger = new UnitType("slinger");
        var ruleset = TestRulesets.With(json => TestRulesets.AddUnit(json, "slinger", movement: 1, sight: 2, cost: 5, "attack"));
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P1, 5, 5);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new SetProductionCommand(city, slinger)).ShouldSucceed();

        update.State.FindCity(city)!.Producing.ShouldBe(slinger);
    }

    [Fact]
    public void Execute_TraderWithoutATradePost_IsRejected()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "trade");
        var plain = builder.AddCity(P1, 5, 5);
        var trading = builder.AddCity(P1, 5, 1, buildings: TestRulesets.TradePost);
        var state = builder.WithBuilding(TestRulesets.TradePost, (6, 1)).Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(plain, TestRulesets.Trader)).ShouldFailWith(RuleViolations.NoTradePost);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(trading, TestRulesets.Trader)).ShouldSucceed();
    }

    [Fact]
    public void Execute_ShipWithoutAHarbour_IsRejected()
    {
        var builder = new GameStateBuilder().WithCapitals().WithNodes(P1, "sailing", "shipbuilding").WithTerrain(TestRulesets.Lake, (7, 1), (7, 5));
        var inland = builder.AddCity(P1, 3, 8);
        var harbour = builder.AddCity(P1, 5, 1, buildings: TestRulesets.Harbour);
        var port = builder.AddCity(P1, 5, 5, buildings: TestRulesets.Port);
        var state = builder.WithBuilding(TestRulesets.Harbour, (6, 1)).WithBuilding(TestRulesets.Port, (6, 5)).Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(inland, TestRulesets.Galley)).ShouldFailWith(RuleViolations.MissingBuilding);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(harbour, TestRulesets.Galley)).ShouldSucceed();
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(port, TestRulesets.Bireme)).ShouldSucceed();
    }

    [Fact]
    public void Execute_ShipBeforeItsScienceItem_IsLocked()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Lake, (7, 1));
        var city = builder.AddCity(P1, 5, 1, buildings: TestRulesets.Harbour);
        var state = builder.WithBuilding(TestRulesets.Harbour, (6, 1)).Build();

        GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Galley)).ShouldFailWith(RuleViolations.Locked);
    }
}
