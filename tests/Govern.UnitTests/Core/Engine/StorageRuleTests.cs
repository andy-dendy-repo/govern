using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// City storage (G8c): every city holds at most its caps of each stocked resource (default ruleset: food 30, water 30, wood 20,
// stone 20, gold 20, culture 40), plus what its working buildings add; the stock is cut at the end of every turn, once that
// turn's income has paid its upkeep and its item's cost.
public class StorageRuleTests
{
    [Fact]
    public void Caps_NoBuildings_AreTheDefaults()
    {
        var caps = CityStorage.Caps(TestRulesets.Default, []);

        caps.ShouldBe([.. TestRulesets.Default.Cities.Storage]);
        CityStorage.CapOf(caps, TestRulesets.Science).ShouldBe(int.MaxValue, "science is a per-turn budget, never stocked");
    }

    [Fact]
    public void Caps_Buildings_AddTheirStorageToTheDefaults()
    {
        var caps = CityStorage.Caps(TestRulesets.Default, [TestRulesets.Workshop, TestRulesets.TradePost]);

        CityStorage.CapOf(caps, TestRulesets.Wood).ShouldBe(20 + 20 + 10);
        CityStorage.CapOf(caps, TestRulesets.Stone).ShouldBe(20 + 20 + 10);
        CityStorage.CapOf(caps, TestRulesets.Food).ShouldBe(30 + 10);
        CityStorage.CapOf(caps, TestRulesets.Water).ShouldBe(30);
    }

    [Fact]
    public void Caps_UpgradedBuilding_CountsOnlyTheUpgrade()
    {
        var caps = CityStorage.Caps(TestRulesets.Default, [TestRulesets.Market]);

        CityStorage.CapOf(caps, TestRulesets.Gold).ShouldBe(20 + 20, "the market replaces the trade post, it doesn't add to it");
    }

    [Fact]
    public void CutAndExcess_StockOverItsCaps_KeepTheCapAndReportTheRest()
    {
        var caps = CityStorage.Caps(TestRulesets.Default, []);
        ResourceAmount[] stock = [new(TestRulesets.Food, 35), new(TestRulesets.Wood, 10)];

        CityStorage.Cut(TestRulesets.Default, stock, caps).ShouldBe([new ResourceAmount(TestRulesets.Food, 30), new ResourceAmount(TestRulesets.Wood, 10)]);
        CityStorage.Excess(TestRulesets.Default, stock, caps).ShouldBe([new ResourceAmount(TestRulesets.Food, 5)]);
    }

    [Fact]
    public void EndTurn_StockOverItsCap_PaysTheItemFirstThenLosesWhatDoesNotFit()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(5, 5, 1));
        var city = builder.AddCity(P1, 5, 5, producing: TestRulesets.Settler, stock: [new(TestRulesets.Food, 35), new(TestRulesets.Water, 10)]);

        var after = EndTurn(builder.Build()).State.FindCity(city)!;

        after.ProductionPaid.ShouldBeTrue("the settler's 4 Food and 2 Water came out of the full stock");
        YieldCalculator.AmountOf(after.StockList, TestRulesets.Food).ShouldBe(30, "35 - 4 = 31, cut to the cap of 30");
        YieldCalculator.AmountOf(after.StockList, TestRulesets.Water).ShouldBe(8);
    }

    [Fact]
    public void EndTurn_BuildingWithStorage_RaisesTheCap()
    {
        var builder = UpkeepRuleTests.DesertCity(out var city, [new(TestRulesets.Wood, 45), new(TestRulesets.Stone, 5)], TestRulesets.Workshop);

        var after = EndTurn(builder.Build()).State.FindCity(city)!;

        YieldCalculator.AmountOf(after.StockList, TestRulesets.Wood).ShouldBe(40, "45 - 1 Wood of upkeep = 44, cut to 20 + 20");
    }

    [Fact]
    public void EndTurn_RuinedBuilding_AddsNoStorage()
    {
        // The barracks' upkeep (1 Food, 1 Gold) can't be paid, so it stays ruined and its +20 Water of storage doesn't count.
        var builder = UpkeepRuleTests.DesertCity(out var city, [new(TestRulesets.Water, 45)], TestRulesets.Barracks);
        builder.WithCity(city, c => c with { BuildingDamage = [new BuildingDamage(TestRulesets.Barracks, TestRulesets.Default.Upkeep.BuildingHealth)] });

        var after = EndTurn(builder.Build()).State.FindCity(city)!;

        YieldCalculator.AmountOf(after.StockList, TestRulesets.Water).ShouldBe(30);
    }

    [Fact]
    public void EndTurn_OtherPlayersCityOverItsCap_IsCutToo()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 8, 5, stock: [new(TestRulesets.Gold, 50)]);

        var after = EndTurn(builder.Build()).State.FindCity(city)!;

        YieldCalculator.AmountOf(after.StockList, TestRulesets.Gold).ShouldBe(20);
    }

    [Fact]
    public void CityForecast_StockOverItsCap_ShowsTheCapsAndWhatIsLost()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Desert, UpkeepRuleTests.Around(5, 5, 1));
        var city = builder.AddCity(P1, 5, 5, producing: TestRulesets.Settler, stock: [new(TestRulesets.Food, 35), new(TestRulesets.Water, 10)]);
        var state = builder.Build();

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city).ShouldNotBeNull();
        var after = EndTurn(state).State.FindCity(city)!;

        forecast.Storage.ShouldBe([.. TestRulesets.Default.Cities.Storage]);
        forecast.Lost.ShouldBe([new ResourceAmount(TestRulesets.Food, 1)]);
        YieldCalculator.AmountOf(after.StockList, TestRulesets.Food).ShouldBe(35 - 4 - 1);
    }

    [Fact]
    public void CityForecast_FullOfCultureThatBuysATile_LosesNothing()
    {
        var builder = UpkeepRuleTests.DesertCity(out var city, [new(TestRulesets.Culture, 40)]);
        var state = builder.Build();

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city).ShouldNotBeNull();
        var after = EndTurn(state).State.FindCity(city)!;

        forecast.Lost.ShouldBeEmpty("40 + 1 - 4 for the next tile = 37 fits");
        YieldCalculator.AmountOf(after.StockList, TestRulesets.Culture).ShouldBe(37);
    }

    [Fact]
    public void Sources_CityWithProductionBuildings_ListsTheCityThenEachBuilding()
    {
        var sources = CityProduction.Sources(TestRulesets.Default, [TestRulesets.Campus, TestRulesets.Workshop, TestRulesets.Barracks]);

        sources.ShouldBe([new ProductionSource(null, 1), new ProductionSource(TestRulesets.Workshop, 1), new ProductionSource(TestRulesets.Barracks, 2)]);
        CityProduction.PerTurn(TestRulesets.Default, [TestRulesets.Campus, TestRulesets.Workshop, TestRulesets.Barracks]).ShouldBe(4);
    }

    [Fact]
    public void CityForecast_ItemCanBegin_ListsWhereItsProductionComesFrom()
    {
        var builder = UpkeepRuleTests.DesertCity(out var city, [new(TestRulesets.Wood, 5)], TestRulesets.Workshop);
        var state = builder.Build();

        var forecast = GameEngine.CityForecast(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), city).ShouldNotBeNull();

        forecast.Sources.ShouldBe([new ProductionSource(null, 1), new ProductionSource(TestRulesets.Workshop, 1)]);
        forecast.ProductionPerTurn.ShouldBe(2);
    }

    private static GameUpdate EndTurn(GameState state) =>
        GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();
}
