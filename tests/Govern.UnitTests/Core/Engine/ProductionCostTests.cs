using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// Resource costs (default ruleset): a soldier costs 2 food and 4 production, an archer 2 wood, an army camp 4 stone and 2 wood.
// A city pays the whole resource cost at the end of the turn its item begins, and only then adds production.
public class ProductionCostTests
{
    [Fact]
    public void EndTurn_StockCoversTheCost_PaysItAndStartsProducing()
    {
        var builder = UnpaidDesertCity(out var city, [new(TestRulesets.Food, 3)]);

        var update = EndTurn(builder.Build());

        var paid = update.State.FindCity(city)!;
        paid.ProductionPaid.ShouldBeTrue();
        paid.ProductionProgress.ShouldBe(1);
        YieldCalculator.AmountOf(paid.StockList, TestRulesets.Food).ShouldBe(1);
    }

    [Fact]
    public void EndTurn_StockShortOfTheCost_WaitsWithoutProgressAndBeginsOnceItCanPay()
    {
        var builder = UnpaidDesertCity(out var city, [new(TestRulesets.Food, 1)]);

        var waiting = EndTurn(builder.Build());

        var unpaid = waiting.State.FindCity(city)!;
        unpaid.ProductionPaid.ShouldBeFalse();
        unpaid.ProductionProgress.ShouldBe(0);
        YieldCalculator.AmountOf(unpaid.StockList, TestRulesets.Food).ShouldBe(1, "nothing is taken while it waits");

        var topped = waiting.State with { Cities = waiting.State.Cities.Replace(unpaid, unpaid with { Stock = [new(TestRulesets.Food, 2)] }) };
        var begun = EndTurn(EndTurn(topped, P2).State).State.FindCity(city)!;
        begun.ProductionPaid.ShouldBeTrue();
        begun.ProductionProgress.ShouldBe(1);
    }

    [Fact]
    public void EndTurn_RepeatedUnit_PaysAgainForTheNextOne()
    {
        var builder = UnpaidDesertCity(out var city, [new(TestRulesets.Food, 2)], progress: 3);
        builder.WithCity(city, c => c with { ProductionPaid = true });

        var finished = EndTurn(builder.Build());
        var next = EndTurn(EndTurn(finished.State, P2).State);

        finished.State.Units.Count(unit => unit.Owner == P1).ShouldBe(1);
        finished.State.FindCity(city)!.ProductionPaid.ShouldBeFalse();
        var again = next.State.FindCity(city)!;
        again.ProductionPaid.ShouldBeTrue();
        YieldCalculator.AmountOf(again.StockList, TestRulesets.Food).ShouldBe(0);
    }

    [Fact]
    public void SetProduction_AfterPaying_RefundsTheCostAndTheSameItemKeepsIt()
    {
        var builder = UnpaidDesertCity(out var city, []);
        builder.WithCity(city, c => c with { ProductionPaid = true, ProductionProgress = 2 });
        var state = builder.Build();

        var same = GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Soldier)).ShouldSucceed();
        var switched = GameEngine.Execute(TestRulesets.Default, state, P1, new SetProductionCommand(city, TestRulesets.Archer)).ShouldSucceed();
        var building = GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.ArmyCamp)).ShouldSucceed();

        same.State.FindCity(city)!.ProductionPaid.ShouldBeTrue();
        same.State.FindCity(city)!.StockList.ShouldBeEmpty();
        var refunded = switched.State.FindCity(city)!;
        refunded.ProductionPaid.ShouldBeFalse();
        refunded.ProductionProgress.ShouldBe(2);
        refunded.StockList.ShouldBe([new ResourceAmount(TestRulesets.Food, 2)]);
        building.State.FindCity(city)!.StockList.ShouldBe([new ResourceAmount(TestRulesets.Food, 2)]);
    }

    [Fact]
    public void ProduceBuilding_FromAPaidBuilding_RefundsItsCost()
    {
        var builder = UnpaidDesertCity(out var city, []);
        builder.WithCity(city, c => c with { ProducingBuilding = TestRulesets.ArmyCamp, ProductionPaid = true });
        var state = builder.Build();

        var same = GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.ArmyCamp)).ShouldSucceed();
        var other = GameEngine.Execute(TestRulesets.Default, state, P1, new ProduceBuildingCommand(city, TestRulesets.Campus)).ShouldSucceed();

        same.State.FindCity(city)!.ProductionPaid.ShouldBeTrue();
        var refunded = other.State.FindCity(city)!;
        refunded.ProductionPaid.ShouldBeFalse();
        refunded.StockList.ShouldBe([new ResourceAmount(TestRulesets.Wood, 2), new ResourceAmount(TestRulesets.Stone, 4)]);
    }

    [Fact]
    public void EndTurn_Building_PaysItsCostWhenItBegins()
    {
        var builder = UnpaidDesertCity(out var city, [new(TestRulesets.Stone, 4), new(TestRulesets.Wood, 1)]);
        builder.WithCity(city, c => c with { ProducingBuilding = TestRulesets.ArmyCamp });

        var waiting = EndTurn(builder.Build());

        waiting.State.FindCity(city)!.ProductionPaid.ShouldBeFalse("2 wood are needed");
        waiting.State.FindCity(city)!.ProductionProgress.ShouldBe(0);
    }

    [Fact]
    public void Capture_LosesThePaidCost()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 6, 5, progress: 2, paid: true);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(6, 5))).ShouldSucceed();

        update.State.FindCity(city)!.ProductionPaid.ShouldBeFalse();
        update.State.FindCity(city)!.StockList.ShouldBeEmpty();
    }

    // A desert city producing soldiers, so its only income is its culture.
    private static GameStateBuilder UnpaidDesertCity(out CityId city, ResourceAmount[] stock, int progress = 0)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(5, 5, 1));
        city = builder.AddCity(P1, 5, 5, progress: progress, stock: stock.Length == 0 ? null : [.. stock]);
        return builder;
    }

    private static GameUpdate EndTurn(GameState state, PlayerId? player = null) =>
        GameEngine.Execute(TestRulesets.Default, state, player ?? P1, new EndTurnCommand()).ShouldSucceed();
}
