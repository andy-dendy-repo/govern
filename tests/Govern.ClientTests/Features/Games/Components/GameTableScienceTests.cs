using System.Text.Json.Nodes;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Economy;
using Govern.Core.Science;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components.Web;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The science counter and tree, production lists under science locks, and the unit and building upgrades.</summary>
public class GameTableScienceTests : ClientTestContext
{
    private static readonly UnitType SwordsmanType = new("swordsman");

    private static readonly BuildingType University = new("university");

    private readonly FakeBoardRenderer _board;

    public GameTableScienceTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void Counter_ShowsUsedOfIncome_AndOpensTheTreeThatEscapeCloses()
    {
        var cut = RenderTable(WithNodes(Create(cities: [City(7, You, At(2, 2))]), 3, "archery"));

        cut.Find(".science-toggle").TextContent.ShouldContain("/ 3");
        cut.Find(".science-toggle .cost-amount").TextContent.ShouldBe("1");
        cut.FindAll(".science-tree").ShouldBeEmpty();

        cut.Find(".science-toggle").Click();

        cut.Find(".science-budget").TextContent.ShouldBe("Your items take 1 of the 3 science your cities make a turn; 2 free.");
        cut.FindAll(".science-node").Count.ShouldBe(Ruleset.Science.Nodes.Length);
        cut.Find(".science-node-archery").ClassList.ShouldContain("node-working");

        cut.Find(".game-screen").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".science-tree").ShouldBeEmpty();
    }

    [Fact]
    public void Tree_PlacesNodesInColumnsByTheirPrerequisites()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        cut.Find(".science-toggle").Click();

        var columns = cut.FindAll(".tree-column");
        columns.Count.ShouldBe(2);
        columns[0].QuerySelector(".science-node-archery").ShouldNotBeNull();
        columns[1].QuerySelector(".science-node-machinery").ShouldNotBeNull();
        cut.Find(".science-node-machinery .node-requires").TextContent.ShouldBe("Builds on Archery");
        cut.Find(".science-node-iron_working .node-unlocks").TextContent.ShouldContain("Pikeman, replaces the Spearman");
        cut.Find(".science-node-education .node-unlocks").TextContent.ShouldContain("University, a builder upgrades the Campus");
    }

    [Fact]
    public void Unlock_AvailableNode_SendsTheCommand()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var cut = RenderTable(WithNodes(Create(cities: [City(7, You, At(2, 2))]), 2));
        cut.Find(".science-toggle").Click();

        cut.Find(".science-node-archery button.unlock-node").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe("""{"type":"unlock_node","node":"archery"}"""));
    }

    [Fact]
    public void Unlock_NotPossible_IsDisabledWithTheReason()
    {
        var cut = RenderTable(WithNodes(Create(cities: [City(7, You, At(2, 2))]), 1, "archery"));

        cut.Find(".science-toggle").Click();

        cut.Find(".science-node-sailing button.unlock-node").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".science-node-sailing .node-reason").TextContent.ShouldBe("You don't have enough free science per turn.");
        cut.Find(".science-node-stirrups .node-reason").TextContent.ShouldBe("You need the science items it builds on first.");
    }

    [Fact]
    public void Tree_SwitchedOffNode_SaysSo()
    {
        var view = Create(cities: [City(7, You, At(2, 2))]) with { Science = new ScienceView(0, [new OwnedNode("archery", Disabled: true)]) };
        var cut = RenderTable(view);

        cut.Find(".science-toggle").Click();

        cut.Find(".science-node-archery").ClassList.ShouldContain("node-off");
        cut.Find(".science-node-archery .node-state").TextContent.ShouldBe("Switched off: your cities make too little science for it.");
        cut.FindAll(".science-node-archery button.unlock-node").ShouldBeEmpty();
    }

    [Fact]
    public void CityUnits_LeaveOutReplacedUnits_AndDisableLockedOnes()
    {
        var cut = RenderTable(WithNodes(Create(cities: [City(7, You, At(2, 2))]), 1, "bronze_working"));

        _board.Click(At(2, 2));

        cut.FindAll("button.produce-soldier").ShouldBeEmpty();
        cut.Find("button.produce-swordsman").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("button.produce-archer").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("button.produce-archer").GetAttribute("title").ShouldBe("A science item must unlock this first.");
    }

    [Fact]
    public void CityUnits_ShipWithoutAHarbour_IsDisabledWithTheReason()
    {
        var inland = City(7, You, At(2, 2));
        var harbour = City(8, You, At(6, 2)) with { Buildings = [Harbour] };
        var cut = RenderTable(WithNodes(Create(cities: [inland, harbour]), 1, "sailing"));

        _board.Click(At(2, 2));

        cut.Find("button.produce-galley").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("button.produce-galley").GetAttribute("title").ShouldBe("Needs a Harbour");

        _board.Click(At(6, 2));

        cut.Find("button.produce-galley").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void CityBuildings_UpgradeHasNoButton_AndItsBuildingCountsAsBuilt()
    {
        var city = City(7, You, At(2, 2)) with { Buildings = [University] };
        var cut = RenderTable(WithNodes(Create(cities: [city]), 4, "education"));

        _board.Click(At(2, 2));

        cut.FindAll("button.produce-building-university").ShouldBeEmpty();
        cut.FindAll("button.produce-building-campus").ShouldBeEmpty("a city with a University has a Campus in every sense");
        cut.Find(".buildings .building-university").TextContent.ShouldContain("University");
    }

    [Fact]
    public void UpgradeUnit_InTerritoryWithTheCost_SendsTheCommand()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var city = City(7, You, At(2, 2), stock: [new ResourceAmount(Stone, 2)]);
        var cut = RenderTable(WithNodes(Create([Soldier(1, You, At(3, 2))], [city]), 1, "bronze_working"));

        _board.Click(At(3, 2));
        cut.Find("button.upgrade-unit").TextContent.ShouldContain("Upgrade to Swordsman");
        cut.Find("button.upgrade-unit").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe("""{"type":"upgrade_unit","unitId":{"value":1}}"""));
    }

    [Fact]
    public void UpgradeUnit_OutsideTerritory_IsDisabledWithTheReason()
    {
        var cut = RenderTable(WithNodes(Create([Soldier(1, You, At(6, 4))], [City(7, You, At(2, 2))]), 1, "bronze_working"));

        _board.Click(At(6, 4));

        cut.Find("button.upgrade-unit").HasAttribute("disabled").ShouldBeTrue();
        cut.Find(".upgrade-unit-reason").TextContent.ShouldBe("Upgrades are made on your cities' territory.");
    }

    [Fact]
    public void UpgradeUnit_NoNewerVersion_HasNoButton()
    {
        var cut = RenderTable(Create([Soldier(1, You, At(3, 2))], [City(7, You, At(2, 2))]));

        _board.Click(At(3, 2));

        cut.FindAll("button.upgrade-unit").ShouldBeEmpty();
    }

    [Fact]
    public void UpgradeBuilding_BuilderOnACampus_SendsTheCommand()
    {
        Api.On(HttpMethod.Post, TestGames.CommandsPath(), () => FakeApiHandler.Json(TestGames.Started()));
        var city = City(7, You, At(2, 2)) with { Buildings = [Campus] };
        var view = Create([Unit(1, You, BuilderType, At(2, 3))], [city], buildings: static coord => coord == At(2, 3) ? Campus : null);
        var cut = RenderTable(WithNodes(view, 2, "education"));

        _board.Click(At(2, 3));
        cut.Find("button.upgrade-building").TextContent.ShouldContain("Upgrade the Campus to a University");
        cut.Find("button.upgrade-building").Click();

        cut.WaitForAssertion(() => Command(0)["command"]!.ToJsonString().ShouldBe("""{"type":"upgrade_building","unitId":{"value":1}}"""));
    }

    [Fact]
    public void UpgradeBuilding_JobUnderWay_ShowsItsProgress()
    {
        var builder = Unit(1, You, BuilderType, At(2, 3)) with { Job = UnitJob.ForUpgrade(University, 1) with { Progress = 3 } };
        var city = City(7, You, At(2, 2)) with { Buildings = [Campus] };
        var view = Create([builder], [city], buildings: static coord => coord == At(2, 3) ? Campus : null);
        var cut = RenderTable(WithNodes(view, 2, "education"));

        _board.Click(At(2, 3));

        cut.FindAll("button.upgrade-building").ShouldBeEmpty();
        cut.Find(".action-hint").TextContent.ShouldStartWith("Upgrading the building here to a University.");
        cut.Markup.ShouldContain($"Upgrading to a University: 3 of {Ruleset.Building(University).Cost}");
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private JsonNode Command(int index) => JsonNode.Parse(Api.RequestsTo(TestGames.CommandsPath())[index].Body!)!;
}
