using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// The upkeep previews the client shows (GameEngine.CityForecast, GameEngine.UnitSupply) decide exactly what the end of turn does.
public class UpkeepPreviewTests
{
    [Theory]
    [InlineData(UpkeepPriority.BuildingsFirst)]
    [InlineData(UpkeepPriority.UnitsFirst)]
    public void Previews_MixedCityAndUnits_MatchTheEndOfTurn(UpkeepPriority priority)
    {
        var builder = UpkeepRuleTests.DesertCity(out var city, [new(TestRulesets.Food, 2), new(TestRulesets.Water, 1), new(TestRulesets.Gold, 1)], TestRulesets.ArmyCamp, TestRulesets.Campus);
        builder.WithCity(city, c => c with { UpkeepPriority = priority })
            .WithTerrain(TestRulesets.Lake, (4, 4))
            .WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(8, 7, 1));
        UnitId[] units =
        [
            builder.AddUnit(P1, TestRulesets.Soldier, 6, 5),
            builder.AddUnit(P1, TestRulesets.Horseman, 4, 5, damage: 20),
            builder.AddUnit(P1, TestRulesets.Soldier, 8, 7),
            builder.AddUnit(P1, TestRulesets.Archer, 8, 3),
        ];
        var state = builder.Build();
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        var forecast = GameEngine.CityForecast(TestRulesets.Default, view, city).ShouldNotBeNull();
        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        var starved = update.Events.OfType<UnitStarvedEvent>().Select(static e => e.UnitId).ToHashSet();
        foreach (var unit in units)
        {
            GameEngine.UnitSupply(TestRulesets.Default, view, unit).ShouldNotBeNull().Supplied.ShouldBe(!starved.Contains(unit), $"unit {unit.Value}");
        }

        forecast.Upkeep.UnpaidBuildings.ShouldBe([.. update.Events.OfType<BuildingDamagedEvent>().Select(static e => e.Building)], ignoreOrder: true);
        update.State.FindCity(city)!.StockList.ShouldBe(forecast.Upkeep.Stock, "its production is paid and its culture buys no tile yet");
        starved.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void CityForecast_MissingToBegin_TellsWhetherProductionBegins(int food, bool begins)
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(5, 5, 1));
        var city = builder.AddCity(P1, 5, 5, stock: [new ResourceAmount(TestRulesets.Food, food)]);
        var state = builder.Build();

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city).ShouldNotBeNull();
        var after = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed().State.FindCity(city)!;

        forecast.MissingToBegin.IsEmpty.ShouldBe(begins);
        after.ProductionPaid.ShouldBe(begins);
        if (!begins)
        {
            forecast.MissingToBegin.ShouldBe([new ResourceAmount(TestRulesets.Food, 1)]);
        }
    }

    [Fact]
    public void Previews_OtherPlayersCityOrUnit_AreNull()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 6, 5);
        var unit = builder.AddUnit(P2, TestRulesets.Soldier, 5, 5);
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 5);
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        GameEngine.CityForecast(TestRulesets.Default, view, city).ShouldBeNull();
        GameEngine.UnitSupply(TestRulesets.Default, view, unit).ShouldBeNull();
    }

    [Fact]
    public void UnitSupply_InDesertBesideARiver_NamesTheMissingFood()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(6, 6, 1))
            .WithRiver(TestRulesets.Stream, ((6, 6), (7, 6)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6);

        var supply = GameEngine.UnitSupply(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1), unit).ShouldNotBeNull();

        supply.City.ShouldBeNull();
        supply.Shortfall.ShouldBe([new ResourceAmount(TestRulesets.Food, 1)]);
    }

    [Theory]
    [InlineData(90, false)]
    [InlineData(100, true)]
    public void CityForecast_ProductionPerTurn_CountsTheBuildingsWorkingAfterUpkeep(int damage, bool paysTheCamp)
    {
        // The army camp (+1 production) costs 1 food a turn: it falls to no health unpaid, or recovers when paid.
        ResourceAmount[] stock = paysTheCamp ? [new(TestRulesets.Food, 1)] : [];
        var builder = UpkeepRuleTests.DesertCity(out var city, stock, TestRulesets.ArmyCamp);
        builder.WithCity(city, c => c with { BuildingDamage = [new BuildingDamage(TestRulesets.ArmyCamp, damage)] });
        var state = builder.Build();

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city).ShouldNotBeNull();
        var after = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed().State.FindCity(city)!;

        forecast.ProductionPerTurn.ShouldBe(after.ProductionProgress);
        forecast.ProductionPerTurn.ShouldBe(TestRulesets.Default.Cities.ProductionPerTurn + (paysTheCamp ? 1 : 0));
    }

    [Fact]
    public void CityForecast_CantBegin_GainsNoProduction()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(5, 5, 1));
        var city = builder.AddCity(P1, 5, 5);

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1), city).ShouldNotBeNull();

        forecast.MissingToBegin.ShouldNotBeEmpty();
        forecast.ProductionPerTurn.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnitSupply_OnEnemyTerritory_LivesOffTheLandLikeTheEndOfTurn(bool desert)
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddCity(P2, 6, 5);
        if (desert)
        {
            builder.WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(7, 5, 1));
        }

        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 7, 5);
        var state = builder.Build();
        state.Map.TileAt(At(7, 5)).Territory.ShouldNotBeNull("the soldier stands on the enemy city's territory");

        var supply = GameEngine.UnitSupply(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit).ShouldNotBeNull();
        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        supply.City.ShouldBeNull();
        supply.Supplied.ShouldBe(!desert);
        update.Events.OfType<UnitStarvedEvent>().Any(e => e.UnitId == unit).ShouldBe(desert);
    }

    [Fact]
    public void UnitSupply_RiverOnTheOuterEdgeOfItsLand_CountsLikeTheEndOfTurn()
    {
        // The stream runs between a supplying tile and the ring beyond, on an edge that tile may not store itself.
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(6, 6, 2))
            .WithRiver(TestRulesets.Stream, ((7, 6), (8, 6)), ((5, 6), (4, 6)));
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 6, 6);
        var state = builder.Build();

        var supply = GameEngine.UnitSupply(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit).ShouldNotBeNull();
        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        supply.Shortfall.ShouldBe([new ResourceAmount(TestRulesets.Food, 1)], "the stream gives the water, nothing gives food");
        update.Events.OfType<UnitStarvedEvent>().ShouldContain(e => e.UnitId == unit);
    }
}
