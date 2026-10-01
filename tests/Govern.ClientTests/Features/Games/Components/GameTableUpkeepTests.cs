using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>Resource costs, upkeep, supply and the upkeep order on the game screen.</summary>
public class GameTableUpkeepTests : ClientTestContext
{
    private static readonly TerrainType Desert = new("desert");

    public GameTableUpkeepTests() => SignIn();

    [Fact]
    public void SelectOwnCity_UnitOnItsTerritory_ShowsTheUpkeepAndTheNetGain()
    {
        var cut = RenderTable(Create([Soldier(1, You, At(3, 2))], [City(7, You, At(2, 2))]));

        Tile(cut, At(2, 2)).Click();

        // 7 plains tiles give 14 food and 7 water; the soldier takes 1 of each, and the settler's 4 food and 2 water begin it.
        cut.Find(".city-resource-food .city-resource-gain").TextContent.ShouldBe("+9");
        cut.Find(".city-resource-water .city-resource-gain").TextContent.ShouldBe("+4");
        cut.FindAll(".upkeep-due .cost-label").Select(static label => label.GetAttribute("title")).ShouldBe(["1 Food", "1 Water"]);
        cut.FindAll(".upkeep-shortage").ShouldBeEmpty();
    }

    [Fact]
    public void SelectOwnCity_BuildingItCantPayFor_SaysSoAndShowsItsHealth()
    {
        var city = City(7, You, At(2, 2)) with { Buildings = [Campus], BuildingDamage = [new BuildingDamage(Campus, 30)] };
        var cut = RenderTable(Create(cities: [city]));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".upkeep-shortage").TextContent.ShouldBe("Can't pay for Campus: each loses 10 health at the end of the turn.");
        cut.Find(".building-campus .building-health").TextContent.ShouldBe("(70 of 100 health)");
    }

    [Fact]
    public void UpkeepOrder_UnitsClicked_SendsTheCommand()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));
        Tile(cut, At(2, 2)).Click();

        cut.Find("button.upkeep-priority-buildings").HasAttribute("disabled").ShouldBeTrue("it is the current order");
        cut.Find("button.upkeep-priority-units").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString()
            .ShouldBe("""{"type":"set_upkeep_priority","cityId":{"value":7},"priority":"unitsFirst"}"""));
    }

    [Theory]
    [InlineData(false, "Pays 4 food, 2 water at the end of the turn to begin.")]
    [InlineData(true, "Resources paid: 4 food, 2 water.")]
    public void SelectOwnCity_ProductionCost_SaysWhetherItIsPaid(bool paid, string expected)
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2)) with { ProductionPaid = paid }]));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".production-cost").TextContent.ShouldBe(expected);
        cut.FindAll("button.produce-settler .cost-label").Count.ShouldBe(3, "production, food and water");
    }

    [Fact]
    public void SelectOwnCity_InTheDesert_SaysWhatProductionWaitsFor()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 1)])], terrain: static _ => Desert));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".production-cost").TextContent.ShouldBe("Waiting for 3 food, 2 water more to begin.");
    }

    [Fact]
    public void SelectOwnCity_CantPayToBegin_ShowsNoProductionGrowth()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 1)])], terrain: static _ => Desert));

        Tile(cut, At(2, 2)).Click();

        cut.FindAll(".progress-turns").Single().TextContent.ShouldBe("Never at this rate");
    }

    [Fact]
    public void SelectOwnCity_ProductionFeedsAJob_SaysItPaysOnceFree()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(0, 1, Improvement: Farm) };
        var cut = RenderTable(Create([builder], [City(7, You, At(2, 2))]));

        Tile(cut, At(2, 2)).Click();

        cut.Find(".production-cost").TextContent.ShouldBe("Pays 4 food, 2 water to begin once its production is free of the job.");
    }

    [Theory]
    [InlineData(3, 2, false, "Kept up by City 1.")]
    [InlineData(6, 4, false, "Fed by the land around it.")]
    [InlineData(6, 4, true, "The land around it lacks 1 food, 1 water: it loses 10 health at the end of the turn.")]
    public void SelectOwnUnit_ShowsItsUpkeepAndWhereItComesFrom(int column, int row, bool desert, string expected)
    {
        var cut = RenderTable(Create([Soldier(1, You, At(column, row))], [City(7, You, At(2, 2))], terrain: coord => desert && coord.DistanceTo(At(6, 4)) <= 1 ? Desert : Plains));

        Tile(cut, At(column, row)).Click();

        cut.FindAll(".unit-upkeep .cost-label").Count.ShouldBe(2);
        cut.Find(".unit-supply").TextContent.ShouldBe(expected);
        cut.Find(".unit-supply").ClassList.ShouldContain(desert ? "short" : "supplied");
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private static IElement Tile(IRenderedComponent<GameTable> cut, HexCoord coord) =>
        cut.Find($"use.tile[data-q='{coord.Q}'][data-r='{coord.R}']");

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
