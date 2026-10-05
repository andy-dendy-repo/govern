using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class BoardClickResolverTests
{
    private static readonly UnitId YourSettler = new(1);
    private static readonly UnitId YourSoldier = new(2);
    private static readonly UnitId EnemySoldier = new(3);

    [Fact]
    public void Resolve_OwnUnit_SelectsIt()
    {
        BoardClickResolver.Resolve(Ruleset, Start(), BoardSelection.None, At(2, 2)).ShouldBe(new BoardClick(BoardSelection.Of(YourSettler), null));
    }

    [Fact]
    public void Resolve_SelectedUnitAndReachableTile_Moves()
    {
        var selection = BoardSelection.Of(YourSettler);

        var click = BoardClickResolver.Resolve(Ruleset, Start(), selection, At(1, 1));

        click.ShouldBe(new BoardClick(selection, new MoveUnitCommand(YourSettler, At(1, 1))));
    }

    [Fact]
    public void Resolve_SelectedSoldierAndAdjacentEnemy_ShowsTheForecastAndNeverAttacksOnAClick()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        var selection = BoardSelection.Of(YourSoldier);

        var first = BoardClickResolver.Resolve(Ruleset, view, selection, At(4, 2));
        var second = BoardClickResolver.Resolve(Ruleset, view, first.Next, At(4, 2));

        first.ShouldBe(new BoardClick(selection with { Target = At(4, 2) }, null));
        second.ShouldBe(first);
    }

    [Fact]
    public void Resolve_SelectedSoldierAndUndefendedEnemyCity_CapturesAtOnce()
    {
        var view = Create([Soldier(2, You, At(3, 2))], [City(8, Opponent, At(4, 2))]);
        var selection = BoardSelection.Of(YourSoldier);

        var click = BoardClickResolver.Resolve(Ruleset, view, selection, At(4, 2));

        click.ShouldBe(new BoardClick(selection, new AttackCommand(YourSoldier, At(4, 2))));
    }

    [Fact]
    public void Resolve_PendingTargetThenMove_MovesAndDropsTheTarget()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        var selection = BoardSelection.Of(YourSoldier) with { Target = At(4, 2) };

        var click = BoardClickResolver.Resolve(Ruleset, view, selection, At(2, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), new MoveUnitCommand(YourSoldier, At(2, 2))));
    }

    [Fact]
    public void Resolve_ArcherAndEnemyTwoTilesAway_ShowsTheForecast()
    {
        var view = Create([Unit(2, You, ArcherType, At(3, 2)), Soldier(3, Opponent, At(5, 2))]);

        var click = BoardClickResolver.Resolve(Ruleset, view, BoardSelection.Of(YourSoldier), At(5, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier) with { Target = At(5, 2) }, null));
    }

    [Fact]
    public void Resolve_OpponentsTurn_OnlySelects()
    {
        var view = Start() with { CurrentPlayer = Opponent };

        BoardClickResolver.Resolve(Ruleset, view, BoardSelection.Of(YourSettler), At(1, 1)).ShouldBe(new BoardClick(BoardSelection.None, null));
        BoardClickResolver.Resolve(Ruleset, view, BoardSelection.None, At(3, 2)).ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), null));
    }

    [Fact]
    public void Resolve_FinishedGame_OnlySelects()
    {
        var view = Finished(You);

        BoardClickResolver.Resolve(Ruleset, view, BoardSelection.Of(YourSettler), At(1, 1)).Command.ShouldBeNull();
    }

    [Fact]
    public void Resolve_SelectedUnitOnOwnCity_TogglesBetweenThem()
    {
        var view = Create([Settler(1, You, At(2, 2))], [City(7, You, At(2, 2))]);

        var toCity = BoardClickResolver.Resolve(Ruleset, view, BoardSelection.Of(YourSettler), At(2, 2));
        var toUnit = BoardClickResolver.Resolve(Ruleset, view, toCity.Next, At(2, 2));

        toCity.ShouldBe(new BoardClick(BoardSelection.Of(new CityId(7)), null));
        toUnit.ShouldBe(new BoardClick(BoardSelection.Of(YourSettler), null));
    }

    [Fact]
    public void Resolve_UnitsSharingATile_AreSelectedInTurnThenTheCity()
    {
        var view = Create([Unit(5, You, TraderType, At(2, 2)), Soldier(2, You, At(2, 2))], [City(7, You, At(2, 2))]);

        var first = BoardClickResolver.Resolve(Ruleset, view, BoardSelection.None, At(2, 2));
        var second = BoardClickResolver.Resolve(Ruleset, view, first.Next, At(2, 2));
        var third = BoardClickResolver.Resolve(Ruleset, view, second.Next, At(2, 2));

        first.Next.ShouldBe(BoardSelection.Of(new UnitId(2)));
        second.Next.ShouldBe(BoardSelection.Of(new UnitId(5)));
        third.Next.ShouldBe(BoardSelection.Of(new CityId(7)));
    }

    [Fact]
    public void Resolve_SelectedSoldierAndALoneEnemyTrader_ShowsTheForecastThenMovesOntoIt()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Unit(6, Opponent, TraderType, At(4, 2))]);
        var selection = BoardSelection.Of(new UnitId(2));

        var first = BoardClickResolver.Resolve(Ruleset, view, selection, At(4, 2));
        var second = BoardClickResolver.Resolve(Ruleset, view, first.Next, At(4, 2));

        first.ShouldBe(new BoardClick(selection with { Target = At(4, 2) }, null));
        second.ShouldBe(new BoardClick(selection, new MoveUnitCommand(new UnitId(2), At(4, 2))));
    }

    [Fact]
    public void Resolve_EnemyUnitOrCity_SelectsItForInspection()
    {
        var view = Create([Soldier(3, Opponent, At(5, 2))], [City(8, Opponent, At(5, 4))]);

        BoardClickResolver.Resolve(Ruleset, view, BoardSelection.None, At(5, 2)).Next.ShouldBe(BoardSelection.Of(EnemySoldier));
        BoardClickResolver.Resolve(Ruleset, view, BoardSelection.None, At(5, 4)).Next.ShouldBe(BoardSelection.Of(new CityId(8)));
    }

    [Fact]
    public void Resolve_EmptyUnreachableTile_Deselects()
    {
        BoardClickResolver.Resolve(Ruleset, Start(), BoardSelection.Of(YourSettler), At(7, 5)).ShouldBe(new BoardClick(BoardSelection.None, null));
    }

    [Fact]
    public void Prune_UnitStillInView_KeepsIt()
    {
        BoardClickResolver.Prune(Ruleset, Start(), BoardSelection.Of(YourSoldier)).ShouldBe(BoardSelection.Of(YourSoldier));
    }

    [Fact]
    public void Prune_TargetStillInReach_KeepsIt()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);
        var selection = BoardSelection.Of(YourSoldier) with { Target = At(4, 2) };

        BoardClickResolver.Prune(Ruleset, view, selection).ShouldBe(selection);
    }

    [Fact]
    public void Prune_AttackerSpentOrTargetGone_DropsOnlyTheTarget()
    {
        var spent = Create([Soldier(2, You, At(3, 2), movementLeft: 0), Soldier(3, Opponent, At(4, 2))]);
        var gone = Create([Soldier(2, You, At(3, 2))]);
        var selection = BoardSelection.Of(YourSoldier) with { Target = At(4, 2) };

        BoardClickResolver.Prune(Ruleset, spent, selection).ShouldBe(BoardSelection.Of(YourSoldier));
        BoardClickResolver.Prune(Ruleset, gone, selection).ShouldBe(BoardSelection.Of(YourSoldier));
    }

    [Fact]
    public void Prune_UnitOrCityGone_DropsIt()
    {
        var view = Create([Settler(1, You, At(2, 2))]);

        BoardClickResolver.Prune(Ruleset, view, BoardSelection.Of(YourSoldier)).ShouldBe(BoardSelection.None);
        BoardClickResolver.Prune(Ruleset, view, BoardSelection.Of(new CityId(7))).ShouldBe(BoardSelection.None);
    }

    [Fact]
    public void Highlights_OwnUnit_AreTheCorePreviews()
    {
        var view = Create([Soldier(2, You, At(3, 2)), Soldier(3, Opponent, At(4, 2))]);

        var highlights = BoardClickResolver.Highlights(Ruleset, view, BoardSelection.Of(YourSoldier));

        highlights.Reachable.ShouldBe(GameEngine.ReachableTiles(Ruleset, view, YourSoldier), ignoreOrder: true);
        highlights.Reachable.ShouldNotBeEmpty();
        highlights.Attackable.ShouldBe([At(4, 2)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Highlights_NoUnitOrEnemyUnit_AreEmpty(int unit)
    {
        var selection = unit == 0 ? BoardSelection.None : BoardSelection.Of(new UnitId(unit));

        var highlights = BoardClickResolver.Highlights(Ruleset, Start(), selection);

        highlights.Reachable.ShouldBeEmpty();
        highlights.Attackable.ShouldBeEmpty();
    }

    [Fact]
    public void Highlights_OpponentsTurn_AreEmpty()
    {
        var highlights = BoardClickResolver.Highlights(Ruleset, Start() with { CurrentPlayer = Opponent }, BoardSelection.Of(YourSettler));

        highlights.Reachable.ShouldBeEmpty();
        highlights.Attackable.ShouldBeEmpty();
    }

    [Fact]
    public void Highlights_FinishedGame_AreEmpty()
    {
        var view = Start() with { Status = GameStatus.Finished };

        BoardClickResolver.Highlights(Ruleset, view, BoardSelection.Of(YourSettler)).Reachable.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_DestinationModeAndAFarTile_OrdersTheWalkAndLeavesTheMode()
    {
        var selection = BoardSelection.Of(YourSoldier) with { Destination = true };

        var click = BoardClickResolver.Resolve(Ruleset, Create([Soldier(2, You, At(3, 2))]), selection, At(7, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), new SetDestinationCommand(YourSoldier, At(7, 2))));
    }

    [Fact]
    public void Resolve_DestinationModeAndAnUnexploredTile_OnlyLeavesTheMode()
    {
        var view = Create([Soldier(2, You, At(3, 2))], visibility: static coord => coord == At(7, 2) ? TileVisibility.Unexplored : TileVisibility.Visible);

        var click = BoardClickResolver.Resolve(Ruleset, view, BoardSelection.Of(YourSoldier) with { Destination = true }, At(7, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), null));
    }

    [Fact]
    public void Highlights_UnitWithADestination_IncludeItsRoute()
    {
        var view = Create([Soldier(2, You, At(3, 2), movementLeft: 0) with { Destination = At(6, 2) }]);

        var route = BoardClickResolver.Highlights(Ruleset, view, BoardSelection.Of(YourSoldier)).Route.ShouldNotBeNull();

        route.Path.ShouldBe([At(4, 2), At(5, 2), At(6, 2)]);
        route.Turns.ShouldBe(2);
    }

    [Fact]
    public void Prune_DestinationModeOnTheOpponentsTurn_LeavesTheMode()
    {
        var view = Create([Soldier(2, You, At(3, 2))]) with { CurrentPlayer = Opponent };

        BoardClickResolver.Prune(Ruleset, view, BoardSelection.Of(YourSoldier) with { Destination = true }).ShouldBe(BoardSelection.Of(YourSoldier));
    }

    [Fact]
    public void Resolve_BridgeModeAndATileAcrossTheRiver_BuildsTheBridgeAndLeavesBridgeMode()
    {
        var selection = BoardSelection.Of(YourSoldier) with { Bridge = true };

        var click = BoardClickResolver.Resolve(Ruleset, RiverView(), selection, At(4, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), new BuildBridgeCommand(YourSoldier, At(4, 2))));
    }

    [Fact]
    public void Resolve_BridgeModeAndAnyOtherTile_OnlyLeavesBridgeMode()
    {
        var selection = BoardSelection.Of(YourSoldier) with { Bridge = true };

        var click = BoardClickResolver.Resolve(Ruleset, RiverView(), selection, At(3, 1));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourSoldier), null));
    }

    [Fact]
    public void Highlights_BridgeMode_AreOnlyTheBridgeTargets()
    {
        var highlights = BoardClickResolver.Highlights(Ruleset, RiverView(), BoardSelection.Of(YourSoldier) with { Bridge = true });

        highlights.Bridgeable.ShouldBe([At(4, 2)]);
        highlights.Reachable.ShouldBeEmpty();
        highlights.Attackable.ShouldBeEmpty();
    }

    [Fact]
    public void Prune_BridgeModeWithNothingToBridge_LeavesBridgeMode()
    {
        var selection = BoardSelection.Of(YourSoldier) with { Bridge = true };

        BoardClickResolver.Prune(Ruleset, Create([Soldier(2, You, At(3, 2))]), selection).ShouldBe(BoardSelection.Of(YourSoldier));
    }

    [Fact]
    public void Resolve_PlacingAndAFreeTileOfTheTerritory_OrdersTheBuildingThereAndStopsPlacing()
    {
        var selection = BoardSelection.Of(YourCity) with { Placing = Campus };

        var click = BoardClickResolver.Resolve(Ruleset, CityView(), selection, At(3, 2));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourCity), new ProduceBuildingCommand(YourCity, Campus, At(3, 2))));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(5, 4)]
    public void Resolve_PlacingAndTheCityTileATakenTileOrOneOutside_OnlyStopsPlacing(int column, int row)
    {
        var selection = BoardSelection.Of(YourCity) with { Placing = Campus };

        var click = BoardClickResolver.Resolve(Ruleset, CityView(), selection, At(column, row));

        click.ShouldBe(new BoardClick(BoardSelection.Of(YourCity), null));
    }

    [Fact]
    public void Highlights_Placing_AreTheFreeTilesOfTheTerritory()
    {
        var highlights = BoardClickResolver.Highlights(Ruleset, CityView(), BoardSelection.Of(YourCity) with { Placing = Campus });

        highlights.Placeable.ShouldBe([At(3, 2), At(1, 2), At(2, 1), At(1, 3), At(2, 3)], ignoreOrder: true);
        highlights.Reachable.ShouldBeEmpty();
    }

    [Fact]
    public void Prune_PlacingABuildingNoTileCanTake_StopsPlacing()
    {
        var selection = BoardSelection.Of(YourCity) with { Placing = Harbour };

        BoardClickResolver.Prune(Ruleset, CityView(), selection).ShouldBe(BoardSelection.Of(YourCity));
    }

    // Your city 7 at (2, 2), with a farm on (1, 1) in its territory.
    private static PlayerView CityView() =>
        Create(cities: [City(7, You, At(2, 2))], improvements: static coord => coord == At(1, 1) ? Farm : null);

    private static readonly CityId YourCity = new(7);

    private static PlayerView RiverView() =>
        Create([Soldier(2, You, At(3, 2))], rivers: static coord => coord == At(3, 2) ? new TileRivers(East: River) : null);
}
