using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Govern.Core.Economy;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The city panel's storage caps and where its production comes from (G8c).</summary>
public class GameTableStorageTests : ClientTestContext
{
    private static readonly BuildingType Workshop = new("workshop");
    private static readonly ResourceType Water = new("water");
    private static readonly ResourceType Wood = new("wood");

    private readonly FakeBoardRenderer _board;

    public GameTableStorageTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void SelectOwnCity_RoomLeft_ShowsEachResourceAgainstItsCap()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 5)])]));

        _board.Click(At(2, 2));

        cut.Find(".city-resource-food .city-resource-cap").TextContent.ShouldBe("/30");
        cut.Find(".city-resource-food").ClassList.ShouldNotContain("city-resource-full");
        cut.FindAll(".city-resource-lost").ShouldBeEmpty();
    }

    [Fact]
    public void SelectOwnCity_IncomeOverflowsItsCap_MarksItFullAndSaysWhatIsLost()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 30), new ResourceAmount(Water, 5)])]));

        _board.Click(At(2, 2));

        // 30 + 14 from the plains - 4 to begin the settler = 40, 10 over the cap of 30.
        cut.Find(".city-resource-food").ClassList.ShouldContain("city-resource-full");
        cut.Find(".city-resource-food .city-resource-lost").TextContent.ShouldBe("10 lost");
        cut.Find(".city-resource-food").GetAttribute("title").ShouldBe("30 of 30 Food the city can hold; 10 won't fit at the end of the turn and is lost");
    }

    [Fact]
    public void SelectOwnCity_ProductionFeedsAJob_CountsTheUnpaidCostAsLost()
    {
        var builder = Unit(1, You, BuilderType, At(3, 2), movementLeft: 0) with { Job = new UnitJob(0, 3, Improvement: Farm) };
        var cut = RenderTable(Create([builder], [City(7, You, At(2, 2), stock: [new ResourceAmount(Food, 30), new ResourceAmount(Water, 5)])]));

        _board.Click(At(2, 2));

        // 30 + 14 from the plains - 1 for the builder = 43: the settler's 4 Food aren't paid while the city feeds the job.
        cut.Find(".city-resource-food .city-resource-lost").TextContent.ShouldBe("13 lost");
    }

    [Fact]
    public void SelectOwnCity_WithAWorkshop_ShowsWhereItsProductionComesFrom()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2), stock: [new ResourceAmount(Wood, 5)]) with { Buildings = [Workshop] }]));

        _board.Click(At(2, 2));

        cut.Find(".production-sources").TextContent.ShouldBe("Production 2 a turn: city 1, Workshop 1");
        cut.Find(".city-resource-wood .city-resource-cap").TextContent.ShouldBe("/40", "the workshop holds 20 more wood");
    }

    [Fact]
    public void SelectOwnCity_BuildingsTab_ShowsTheStorageEachBuildingAdds()
    {
        var cut = RenderTable(Create(cities: [City(7, You, At(2, 2))]));

        _board.Click(At(2, 2));

        cut.FindAll(".building-effect").Select(static effect => effect.TextContent)
            .ShouldContain("+1 production a turn; holds +20 Wood, +20 Stone");
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view)));
}
