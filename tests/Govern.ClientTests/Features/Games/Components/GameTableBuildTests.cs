using System.Text.Json.Nodes;
using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.ClientTests.Support;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components.Web;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>Builders, improvements, bridges, city buildings and the stockpile on the game screen.</summary>
public class GameTableBuildTests : ClientTestContext
{
    private readonly FakeBoardRenderer _board;

    public GameTableBuildTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void BuildImprovement_BuilderInTheTerritory_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(3, 2))], [City(7, You, At(2, 2))]));

        _board.Click(At(3, 2));
        cut.Find("button.build-farm").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"build_improvement","unitId":{"value":1},"improvement":"farm"}"""));
    }

    [Fact]
    public void BuildImprovement_OutsideTheTerritory_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(6, 4))], [City(7, You, At(2, 2))]));

        _board.Click(At(6, 4));

        cut.Find("button.build-farm").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".build-improvement-reason").TextContent.ShouldBe("Improvements can only be built in your cities' territory, off the city tiles.");
    }

    [Fact]
    public void BuildImprovement_NothingFitsTheTerrain_SaysSo()
    {
        var cut = RenderTable(Create([Unit(1, You, BuilderType, At(3, 2))], [City(7, You, At(2, 2))],
            terrain: static coord => coord == At(3, 2) ? new TerrainType("lake") : Plains));

        _board.Click(At(3, 2));

        cut.FindAll("button.build-improvement").ShouldBeEmpty();
        cut.Find(".no-improvement").TextContent.ShouldBe("No improvement fits this terrain.");
    }

    [Fact]
    public void BuildBridge_PickATileAcrossTheRiver_SendsIt()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(RiverView([Soldier(2, You, At(3, 2))]));
        _board.Click(At(3, 2));

        cut.Find("button.build-bridge").Click();
        _board.Tinted("--gv-overlay-bridgeable").Count.ShouldBe(1);
        _board.Tinted("--gv-overlay-reachable").ShouldBeEmpty();
        _board.Click(At(4, 2));

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe($$$"""{"type":"build_bridge","unitId":{"value":2},"target":{"q":{{{At(4, 2).Q}}},"r":{{{At(4, 2).R}}}}}"""));
    }

    [Fact]
    public void BuildBridge_EscapeOrAnotherClick_LeavesBridgeModeWithoutSending()
    {
        var cut = RenderTable(RiverView([Soldier(2, You, At(3, 2))]));
        _board.Click(At(3, 2));
        cut.Find("button.build-bridge").Click();

        _board.Click(At(2, 2));

        _board.Tinted("--gv-overlay-bridgeable").ShouldBeEmpty();
        cut.Find("button.build-bridge").TextContent.ShouldContain("Build bridge");
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
    }

    [Fact]
    public void BuildBridge_NoRiverNearby_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create([Soldier(2, You, At(3, 2))]));

        _board.Click(At(3, 2));

        cut.Find("button.build-bridge").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".build-bridge-reason").TextContent.ShouldBe("No river next to this unit can be bridged now.");
    }

    [Fact]
    public void Render_UnitWithAJob_ShowsItsProgress()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(2, 1, Improvement: Farm) };
        var cut = RenderTable(Create([builder]));

        _board.Click(At(3, 2));

        cut.Find(".unit-job").TextContent.ShouldBe($"Building a Farm: 2 of {Ruleset.Improvement(Farm).Cost}");
        cut.Find(".unit-panel .build").TextContent.ShouldContain($"{Ruleset.Unit(BuilderType).BuildProduction} per turn");
        cut.Find(".action-hint").TextContent.ShouldStartWith("Building a Farm.");
    }

    [Fact]
    public void Render_CityFeedingAJob_SaysItsProductionIsLent()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(0, 3, Improvement: Farm) };
        var cut = RenderTable(Create([builder], [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        cut.Find(".lent-production").TextContent.ShouldContain("goes to a unit building on its land");
        cut.Find(".city-panel .progress-turns").TextContent.ShouldBe("Never at this rate");
    }

    [Fact]
    public void ProduceBuilding_PickATileOfTheTerritory_SendsItWithTheSite()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));
        _board.Click(At(2, 2));

        cut.Find("button.produce-building-campus").Click();
        _board.Tinted("--gv-overlay-placeable").Count.ShouldBe(6);
        cut.Find(".building-placing").TextContent.ShouldStartWith("Pick a highlighted tile");
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
        _board.Click(At(3, 2));

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe($$$"""{"type":"produce_building","cityId":{"value":7},"building":"campus","site":{"q":{{{At(3, 2).Q}}},"r":{{{At(3, 2).R}}}}}"""));
    }

    [Fact]
    public void ProduceBuilding_ClickOutsideOrTheButtonAgain_StopsPlacingWithoutSending()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));
        _board.Click(At(2, 2));

        cut.Find("button.produce-building-campus").Click();
        _board.Click(At(6, 4));
        _board.Tinted("--gv-overlay-placeable").ShouldBeEmpty();

        _board.Click(At(2, 2));
        cut.Find("button.produce-building-campus").Click();
        cut.Find("button.produce-building-campus").Click();
        _board.Tinted("--gv-overlay-placeable").ShouldBeEmpty();
        cut.FindAll(".building-placing").ShouldBeEmpty();
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();
    }

    [Fact]
    public void ProduceBuilding_MoveTheOneInProduction_SendsTheNewSiteAndItsCurrentSiteOnlyCancels()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var city = City(7, You, At(2, 2)) with { ProducingBuilding = Campus, ProducingBuildingSite = At(3, 2) };
        var cut = RenderTable(Create(cities: [city]));
        _board.Click(At(2, 2));

        cut.Find("button.produce-building-campus").Click();
        _board.Click(At(3, 2));
        _board.Tinted("--gv-overlay-placeable").ShouldBeEmpty();
        Api.RequestsTo(TestGames.CommandsPath()).ShouldBeEmpty();

        cut.Find("button.produce-building-campus").Click();
        _board.Click(At(2, 3));

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe($$$"""{"type":"produce_building","cityId":{"value":7},"building":"campus","site":{"q":{{{At(2, 3).Q}}},"r":{{{At(2, 3).R}}}}}"""));
    }

    [Fact]
    public void ProduceBuilding_PlacingStarts_FocusesTheScreenSoEscapeCancelsIt()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));
        _board.Click(At(2, 2));
        var focusesBefore = Focuses();

        cut.Find("button.produce-building-campus").Click();

        Focuses().ShouldBe(focusesBefore + 1, "the city panel's tabs keep Esc from the screen, so the screen takes focus back");
        cut.Find(".game-screen").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        _board.Tinted("--gv-overlay-placeable").ShouldBeEmpty();
        cut.Find("button.produce-building-campus").ShouldNotBeNull("the city stays selected");
    }

    [Fact]
    public void ProduceBuilding_NeedsALake_IsDisabledWithTheReason()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        cut.Find("button.produce-building-harbour").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".building-reason").TextContent.ShouldBe("This building needs a different terrain next to its tile.");
    }

    [Fact]
    public void Render_CityWithBuildings_ListsThemAndDrawsThemOnTheirTiles()
    {
        var city = City(7, You, At(2, 2)) with { ProducingBuilding = Harbour, ProducingBuildingSite = At(1, 1), Buildings = [Campus] };
        var cut = RenderTable(Create(
            cities: [city],
            terrain: static coord => coord == At(1, 2) ? new TerrainType("lake") : Plains,
            buildings: static coord => coord == At(2, 3) ? Campus : null));

        _board.Click(At(2, 2));

        cut.Find(".buildings .building-campus").TextContent.ShouldContain("Campus");
        cut.FindAll("button.produce-building-campus").ShouldBeEmpty();
        cut.Find(".production").TextContent.ShouldBe($"Producing a Harbour: 2 of {Ruleset.Building(Harbour).Cost}");
        cut.Find("button.produce-building-harbour").TextContent.ShouldContain("Move Harbour");
        cut.Find("button.produce-settler").HasAttribute("disabled").ShouldBeFalse();
        Feature(At(2, 3)).ShouldBe((Model(FeatureModels.For(Campus)), false));
        Feature(At(1, 1)).ShouldBe((Model(FeatureModels.For(Harbour)), true));
    }

    [Fact]
    public void SelectOwnCity_ShowsItsStockWithTheGainEachTurnAndTheNextTilesPrice()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 5), new ResourceAmount(Culture, 2)])]));

        _board.Click(At(2, 2));

        // 7 plains tiles (2 food, 1 water each) and the city's own culture, less the settler's 4 food and 2 water paid to begin.
        cut.Find(".city-resource-food .cost-amount").TextContent.ShouldBe("5");
        cut.Find(".city-resource-food .city-resource-gain").TextContent.ShouldBe("+10");
        cut.Find(".city-resource-water .cost-amount").TextContent.ShouldBe("0");
        cut.Find(".city-resource-water .city-resource-gain").TextContent.ShouldBe("+5");
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

        cut.WaitForAssertion(() => _board.Scene.Bridges.Count.ShouldBe(1));
        Feature(At(1, 1)).ShouldBe((Model(FeatureModels.For(Farm)), false));
    }

    private static PlayerView RiverView(IEnumerable<UnitView> units) =>
        Create(units, rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    // Element focus (Blazor's FocusAsync), not the board's camera focus.
    private int Focuses() => JSInterop.Invocations.Count(static call => call.Identifier.EndsWith(".focus", StringComparison.Ordinal));

    // The model and planned flag of the improvement or building the board draws on a tile.
    private (string Model, bool Planned) Feature(HexCoord tile) =>
        _board.Scene.Features.Where(feature => feature.Q == tile.Q && feature.R == tile.R)
            .Select(static feature => (feature.Model, feature.Planned)).ShouldHaveSingleItem();

    private static string Model(FeatureModel model) => model.ToString().ToLowerInvariant();

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
