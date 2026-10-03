using AngleSharp.Dom;
using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The tabs of the player's own city panel: Units, Buildings, Economy and Market.</summary>
public class GameTableCityTabsTests : ClientTestContext
{
    private readonly FakeBoardRenderer _board;

    public GameTableCityTabsTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void SelectOwnCity_OpensUnitsWithTheProductionAboveTheTabs()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        TabNames(cut).ShouldBe(["Units", "Buildings", "Economy", "Market"]);
        SelectedTab(cut).ShouldBe("Units");
        cut.Find(".city-panel > .production").TextContent.ShouldStartWith("Producing a Settler");
        cut.Find(".city-tab-units button.produce-soldier").ShouldNotBeNull();
        cut.Find(".city-tab-buildings button.produce-building-campus").ShouldNotBeNull();
        cut.Find(".city-tab-economy .city-upkeep").ShouldNotBeNull();
        cut.Find(".city-tab-economy .territory-growth").ShouldNotBeNull();
    }

    [Fact]
    public void Tab_Clicked_OpensItAndStaysOpenForTheNextCity()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2)), City(8, You, At(6, 4), "Second")]));
        _board.Click(At(2, 2));

        Tab(cut, "Buildings").Click();
        SelectedTab(cut).ShouldBe("Buildings");
        _board.Click(At(6, 4));

        cut.Find(".city-panel .city-name").TextContent.ShouldBe("Second");
        SelectedTab(cut).ShouldBe("Buildings");
    }

    [Fact]
    public void SelectOwnCity_ItCantPayItsUpkeep_MarksTheEconomyTab()
    {
        var city = City(7, You, At(2, 2)) with { Buildings = [Campus] };
        var cut = RenderTable(Create(cities: [city]));

        _board.Click(At(2, 2));

        Tab(cut, "Economy").QuerySelector(".rz-tabview-icon")!.TextContent.ShouldBe("warning");
        Tab(cut, "Units").QuerySelector(".rz-tabview-icon").ShouldBeNull();
    }

    [Fact]
    public void SelectOwnCity_PaysItsUpkeep_LeavesTheEconomyTabUnmarked()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        Tab(cut, "Economy").QuerySelector(".rz-tabview-icon").ShouldBeNull();
    }

    [Fact]
    public void SelectEnemyCity_HasNoTabs()
    {
        var cut = RenderTable(Create(cities: [City(8, Opponent, At(5, 4), "Aria")]));

        _board.Click(At(5, 4));

        cut.FindAll(".city-tabs").ShouldBeEmpty();
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));

    private static IEnumerable<string> TabNames(IRenderedComponent<GameTable> cut) =>
        cut.FindAll(".city-tabs [role=tab]").Select(static tab => tab.TextContent.Replace("warning", string.Empty, StringComparison.Ordinal).Trim());

    private static IElement Tab(IRenderedComponent<GameTable> cut, string name) =>
        cut.FindAll(".city-tabs [role=tab]").Single(tab => tab.TextContent.Trim().EndsWith(name, StringComparison.Ordinal));

    private static string SelectedTab(IRenderedComponent<GameTable> cut) =>
        cut.Find(".city-tabs [role=tab][aria-selected=true]").TextContent.Trim();
}
