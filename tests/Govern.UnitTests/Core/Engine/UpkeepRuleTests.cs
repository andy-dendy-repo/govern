using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// Upkeep at the end of the owner's turn (default ruleset): a soldier needs 1 food and 1 water, an army camp 1 food, a campus
// 1 gold. A city pays for its buildings and for the units on its territory from its stock; a unit elsewhere is supplied by the
// tiles within 1. A shortage costs 10 health (a unit keeps at least 1); a paid building recovers 10.
public class UpkeepRuleTests
{
    [Theory]
    [InlineData(UpkeepPriority.BuildingsFirst)]
    [InlineData(UpkeepPriority.UnitsFirst)]
    public void EndTurn_StockForOneItemOnly_PaysWhatThePriorityPutsFirst(UpkeepPriority priority)
    {
        var builder = DesertCity(out var city, stock: [new(TestRulesets.Food, 1), new(TestRulesets.Water, 1)], TestRulesets.ArmyCamp);
        builder.WithCity(city, c => c with { UpkeepPriority = priority });
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5);

        var update = EndTurn(builder.Build());

        var after = update.State.FindCity(city)!;
        if (priority == UpkeepPriority.BuildingsFirst)
        {
            update.State.FindUnit(soldier)!.Damage.ShouldBe(10);
            update.Events.ShouldContain(new UnitStarvedEvent(soldier, 90));
            after.DamageOf(TestRulesets.ArmyCamp).ShouldBe(0);
            YieldCalculator.AmountOf(after.StockList, TestRulesets.Water).ShouldBe(1, "the soldier's upkeep is paid in full or not at all");
        }
        else
        {
            update.State.FindUnit(soldier)!.Damage.ShouldBe(0);
            after.DamageOf(TestRulesets.ArmyCamp).ShouldBe(10);
            update.Events.ShouldContain(new BuildingDamagedEvent(city, TestRulesets.ArmyCamp, 90));
            YieldCalculator.AmountOf(after.StockList, TestRulesets.Water).ShouldBe(0);
        }

        YieldCalculator.AmountOf(after.StockList, TestRulesets.Food).ShouldBe(0);
    }

    [Fact]
    public void EndTurn_TwoUnitsAndUpkeepForOne_PaysTheMostHurtFirst()
    {
        var builder = DesertCity(out _, stock: [new(TestRulesets.Food, 1), new(TestRulesets.Water, 1)]);
        var fresh = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5);
        var hurt = builder.AddUnit(P1, TestRulesets.Soldier, 4, 5, movementLeft: 0, damage: 30);

        var update = EndTurn(builder.Build());

        update.State.FindUnit(hurt)!.Damage.ShouldBe(30, "paid, but it moved, so it doesn't heal");
        update.State.FindUnit(fresh)!.Damage.ShouldBe(10);
    }

    [Fact]
    public void EndTurn_UnpaidItem_TakesNothingAndALaterItemIsStillPaid()
    {
        var builder = DesertCity(out var city, stock: [new(TestRulesets.Food, 1)], TestRulesets.ArmyCamp);
        builder.WithCity(city, c => c with { UpkeepPriority = UpkeepPriority.UnitsFirst });
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 5);

        var update = EndTurn(builder.Build());

        update.State.FindUnit(soldier)!.Damage.ShouldBe(10, "no water");
        update.State.FindCity(city)!.DamageOf(TestRulesets.ArmyCamp).ShouldBe(0);
        update.State.FindCity(city)!.StockList.ShouldBe([new ResourceAmount(TestRulesets.Culture, 1)]);
    }

    [Fact]
    public void EndTurn_UnitOutsideTerritoryOnGreenLand_IsSuppliedAndTakesNothingFromTheStock()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var first = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6);
        var second = builder.AddUnit(P1, TestRulesets.Horseman, 7, 6);

        var update = EndTurn(builder.Build());

        update.Events.OfType<UnitStarvedEvent>().ShouldBeEmpty("tiles are not shared out: both units get the full yield");
        update.State.FindUnit(first)!.Damage.ShouldBe(0);
        update.State.FindUnit(second)!.Damage.ShouldBe(0);
    }

    [Fact]
    public void EndTurn_UnitInDeepDesert_LosesHealth()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, Around(6, 6, 1));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6);

        var update = EndTurn(builder.Build());

        update.State.FindUnit(soldier)!.Damage.ShouldBe(10);
        update.Events.ShouldContain(new UnitStarvedEvent(soldier, 90));
    }

    [Fact]
    public void EndTurn_DesertNextToALake_SuppliesTheUnit()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, Around(6, 6, 1)).WithTerrain(TestRulesets.Lake, (7, 6));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6);

        var update = EndTurn(builder.Build());

        update.State.FindUnit(soldier)!.Damage.ShouldBe(0);
    }

    [Fact]
    public void EndTurn_Shortage_NeverTakesAUnitBelowOneHealth()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, Around(6, 6, 1));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6, damage: 95);

        var first = EndTurn(builder.Build());
        var second = EndTurn(EndTurn(first.State, P2).State);

        first.State.FindUnit(soldier)!.Damage.ShouldBe(99);
        first.Events.ShouldContain(new UnitStarvedEvent(soldier, 1));
        second.State.FindUnit(soldier)!.Damage.ShouldBe(99);
    }

    [Fact]
    public void EndTurn_RestingHurtUnit_HealsOnlyWhenSupplied()
    {
        var green = new GameStateBuilder().WithCapitals();
        var fed = green.AddUnit(P1, TestRulesets.Soldier, 6, 6, damage: 30);
        var desert = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, Around(6, 6, 1));
        var starving = desert.AddUnit(P1, TestRulesets.Soldier, 6, 6, damage: 30);

        EndTurn(green.Build()).State.FindUnit(fed)!.Damage.ShouldBe(20);
        EndTurn(desert.Build()).State.FindUnit(starving)!.Damage.ShouldBe(40);
    }

    [Fact]
    public void EndTurn_BuildingWithNoHealth_GivesNothingUntilItsUpkeepIsPaidAgain()
    {
        var builder = DesertCity(out var city, stock: [new(TestRulesets.Gold, 1)], TestRulesets.Campus);
        builder.WithCity(city, c => c with { BuildingDamage = [new BuildingDamage(TestRulesets.Campus, 100)] });

        var start = builder.Build();
        var first = EndTurn(start);
        var second = EndTurn(EndTurn(first.State, P2).State);

        // Science is counted per turn from the working buildings (the city's own 1, plus 2 from a working campus).
        int Science(GameState state) => GameEngine.ViewFor(TestRulesets.Default, state, P1).Science!.Income;
        Science(start).ShouldBe(2, "the capital and the city make 1 each; at 0 health the campus gives nothing");
        first.State.FindCity(city)!.DamageOf(TestRulesets.Campus).ShouldBe(90, "its upkeep was paid, so it recovers");
        Science(first.State).ShouldBe(4);
        second.State.FindCity(city)!.DamageOf(TestRulesets.Campus).ShouldBe(100, "no gold left for its upkeep");
        Science(second.State).ShouldBe(2);
    }

    [Fact]
    public void SetUpkeepPriority_OwnCity_ChangesItAndAnotherPlayersCityIsNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddCity(P1, 5, 5);
        var enemy = builder.AddCity(P2, 9, 5);
        var state = builder.Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new SetUpkeepPriorityCommand(own, UpkeepPriority.UnitsFirst)).ShouldSucceed();

        update.State.FindCity(own)!.UpkeepPriority.ShouldBe(UpkeepPriority.UnitsFirst);
        update.Events.ShouldBe([new UpkeepPrioritySetEvent(own, UpkeepPriority.UnitsFirst)]);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetUpkeepPriorityCommand(enemy, UpkeepPriority.UnitsFirst)).ShouldFailWith(RuleViolations.CityNotFound);
        GameEngine.Execute(TestRulesets.Default, state, P1, new SetUpkeepPriorityCommand(own, (UpkeepPriority)7)).ShouldFailWith(RuleViolations.UnknownUpkeepPriority);
    }

    // A city at (5, 5) whose territory is desert, so it gains only its own culture; its production is already paid.
    internal static GameStateBuilder DesertCity(out CityId city, ResourceAmount[] stock, params BuildingType[] buildings)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, Around(5, 5, 1));
        city = builder.AddCity(P1, 5, 5, stock: [.. stock], paid: true, buildings: buildings);
        return builder;
    }

    internal static (int Column, int Row)[] Around(int column, int row, int radius) =>
        [.. At(column, row).WithinRadius(radius).Select(static coord => (coord.Column, coord.Row))];

    private static GameUpdate EndTurn(GameState state, PlayerId? player = null) =>
        GameEngine.Execute(TestRulesets.Default, state, player ?? P1, new EndTurnCommand()).ShouldSucceed();
}
