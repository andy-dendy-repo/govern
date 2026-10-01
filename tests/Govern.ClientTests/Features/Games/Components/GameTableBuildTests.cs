using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>Builders, improvements, bridges, city buildings and the stockpile on the game screen.</summary>
public class GameTableBuildTests : ClientTestContext
{
    public GameTableBuildTests() => SignIn();

    [Fact]
    public void BuildImprovement_BuilderInTheTerritory_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(3, 2))], [City(7, You, At(2, 2))]));

        Tile(cut, At(3, 2)).Click();
        cut.Find("button.build-farm").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"build_improvement","unitId":{"value":1},"improvement":"farm"}"""));
    }

    [Fact]
    public void BuildImprovement_OutsideTheTerritory_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(6, 4))], [City(7, You, At(2, 2))]));

        Tile(cut, At(6, 4)).Click();

        cut.Find("button.build-farm").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".build-improvement-reason").TextContent.ShouldBe("Improvements can only be built in your cities' territory, off the city tiles.");
    }

    [Fact]
    public void BuildImprovement_NothingFitsTheTerrain_SaysSo()
    {
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(3, 2))], [City(7, You, At(2, 2))],
            terrain: static coord => coord == At(3, 2) ? new TerrainType("lake") : Plains));

        Tile(cut, At(3, 2)).Click();

        cut.FindAll("button.build-improvement").ShouldBeEmpty();
        cut.Find(".no-improvement").TextContent.ShouldBe("No improvement fits this terrain.");
    }

    [Fact]
    public void BuildBridge_PickATileAcrossTheRiver_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(RiverView([Soldier(2, You, At(3, 2))]));
        Tile(cut, At(3, 2)).Click();

        cut.Find("button.build-bridge").Click();
        cut.FindAll("use.overlay.bridgeable").Count.ShouldBe(1);
        cut.FindAll("use.overlay.reachable").ShouldBeEmpty();
        Tile(cut, At(4, 2)).Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe($$$"""{"type":"build_bridge","unitId":{"value":2},"target":{"q":{{{At(4, 2).Q}}},"r":{{{At(4, 2).R}}}}}"""));
    }

    [Fact]
    public void BuildBridge_EscapeOrAnotherClick_LeavesBridgeModeWithoutSending()
    {
        var cut = RenderTable(RiverView([Soldier(2, You, At(3, 2))]));
        Tile(cut, At(3, 2)).Click();
        cut.Find("button.build-bridge").Click();

        Tile(cut, At(2, 2)).Click();

        cut.FindAll("use.overlay.bridgeable").ShouldBeEmpty();
        cut.Find("button.build-bridge").TextContent.ShouldContain("Build bridge");
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
    }

    [Fact]
    public void BuildBridge_NoRiverNearby_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create([Soldier(2, You, At(3, 2))]));

        Tile(cut, At(3, 2)).Click();

        cut.Find("button.build-bridge").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".build-bridge-reason").TextContent.ShouldBe("No river next to this unit can be bridged now.");
    }

    [Fact]
    public void Render_UnitWithAJob_ShowsItsProgress()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(2, 1, Improvement: Farm) };
        var cut = RenderTable(Create([builder]));

        Tile(cut, At(3, 2)).Click();

        cut.Find(".unit-job").TextContent.ShouldBe($"Building a Farm: 2 of {Ruleset.Improvement(Farm).Cost}");
        cut.Find(".unit-panel .build").TextContent.ShouldContain($"{Ruleset.Unit(BuilderType).BuildProduction} per turn");
        cut.Find(".action-hint").TextContent.ShouldStartWith("Building a Farm.");
    }

    [Fact]
    public void Render_CityFeedingAJob_SaysItsProductionIsLent()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(0, 3, Improvement: Farm) };
        var cut = RenderTable(Create([builder], [City(7, You, At(2, 2))]));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".lent-production").TextContent.ShouldContain("goes to a unit building on its land");
        cut.Find(".city-panel .progress-turns").TextContent.ShouldBe("Never at this rate");
    }

    [Fact]
    public void ProduceBuilding_Clicked_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        Tile(cut, At(2, 2)).Click();
        cut.Find("button.produce-building-campus").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"produce_building","cityId":{"value":7},"building":"campus"}"""));
    }

    [Fact]
    public void ProduceBuilding_NeedsALake_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        Tile(cut, At(2, 2)).Click();

        cut.Find("button.produce-building-harbour").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".building-reason").TextContent.ShouldBe("This building needs a different terrain next to the city.");
    }

    [Fact]
    public void Render_CityWithBuildings_ListsThemAndShowsTheBuildingInProduction()
    {
        var city = City(7, You, At(2, 2)) with { ProducingBuilding = Harbour, Buildings = [Campus] };
        var cut = RenderTable(Create(cities: [city], terrain: static coord => coord == At(1, 2) ? new TerrainType("lake") : Plains));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".buildings .building-campus").TextContent.ShouldContain("Campus");
        cut.FindAll("button.produce-building-campus").ShouldBeEmpty();
        cut.Find(".production").TextContent.ShouldBe($"Producing a Harbour: 2 of {Ruleset.Building(Harbour).Cost}");
        cut.Find("button.produce-building-harbour").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("button.produce-settler").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void SelectOwnCity_ShowsItsStockWithTheGainEachTurnAndTheNextTilesPrice()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 5), new ResourceAmount(Culture, 2)])]));

        Tile(cut, At(2, 2)).Click();

        // 7 plains tiles (2 food, 1 water each) and the city's own culture.
        cut.Find(".city-resource-food .cost-amount").TextContent.ShouldBe("5");
        cut.Find(".city-resource-food .city-resource-gain").TextContent.ShouldBe("+14");
        cut.Find(".city-resource-water .cost-amount").TextContent.ShouldBe("0");
        cut.Find(".city-resource-water .city-resource-gain").TextContent.ShouldBe("+7");
        cut.Find(".city-resource-culture .city-resource-gain").TextContent.ShouldBe("+1");
        cut.FindAll(".city-resource-gold").ShouldBeEmpty("no gold and no gain");
        cut.Find(".territory-growth").TextContent.ShouldBe("Next territory tile: 2 of 4 culture");
    }

    [Fact]
    public void Render_Stockpile_ShowsEveryResourceSummedOverYourCities()
    {
        var cut = RenderTable(Create(cities: [
            City(7, You, At(2, 2), stock: [new ResourceAmount(Science, 7)]),
            City(8, You, At(6, 2), stock: [new ResourceAmount(Science, 3)]),
            City(9, Opponent, At(6, 5)),
        ]));

        cut.FindAll(".stockpile .stock").Count.ShouldBe(Ruleset.Resources.Length);
        cut.Find(".stock-science .cost-amount").TextContent.ShouldBe("10");
        cut.Find(".stock-food .cost-amount").TextContent.ShouldBe("0");
    }

    [Fact]
    public void Render_ImprovementsAndBridges_AreDrawnOnTheBoard()
    {
        var view = Create(
            rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null,
            improvements: static coord => coord == At(1, 1) ? Farm : null,
            bridges: static coord => coord == At(3, 2) ? new TileBridges(East: true) : null);

        var cut = RenderTable(view);

        cut.Find("[data-layer='improvements'] use.improvement-farm").GetAttribute("href").ShouldBe("#improvement-farm");
        cut.FindAll("[data-layer='rivers'] line.bridge").Count.ShouldBe(1);
    }

    private static PlayerView RiverView(IEnumerable<UnitView> units) =>
        Create(units, rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private static IElement Tile(IRenderedComponent<GameTable> cut, HexCoord coord) =>
        cut.Find($"use.tile[data-q='{coord.Q}'][data-r='{coord.R}']");

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
