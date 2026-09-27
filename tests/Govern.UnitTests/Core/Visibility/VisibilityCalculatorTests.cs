using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Visibility;

public class VisibilityCalculatorTests
{
    [Fact]
    public void ViewFor_EnemyOutOfSight_HidesTheirUnitsAndCities()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddUnit(P2, UnitType.Soldier, 9, 7);

        var view = GameEngine.ViewFor(builder.Build(), P1);

        view.Units.ShouldHaveSingleItem().Id.ShouldBe(own);
        view.Cities.ShouldAllBe(city => city.Owner == P1);
    }

    [Fact]
    public void ViewFor_SameState_GivesTheSameNonNegativeVersion()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var first = GameEngine.ViewFor(state, P1);
        var second = GameEngine.ViewFor(state, P1);

        first.Version.ShouldBe(second.Version);
        first.Version.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ViewFor_ChangeTheViewerSees_ChangesTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        var state = builder.Build();

        var moved = GameEngine.Execute(state, P1, new MoveUnitCommand(own, At(3, 2))).ShouldSucceed().State;

        GameEngine.ViewFor(moved, P1).Version.ShouldNotBe(GameEngine.ViewFor(state, P1).Version);
    }

    [Fact]
    public void ViewFor_EnemyMoveOutOfSight_KeepsTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2);
        builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        var enemy = builder.AddUnit(P2, UnitType.Soldier, 9, 7);
        var state = builder.Build();

        var moved = GameEngine.Execute(state, P2, new MoveUnitCommand(enemy, At(8, 7))).ShouldSucceed().State;

        moved.Version.ShouldBe(state.Version + 1);
        GameEngine.ViewFor(moved, P1).Version.ShouldBe(GameEngine.ViewFor(state, P1).Version);
    }

    [Fact]
    public void ViewFor_UnitsAndCities_AreOrderedByIdNotByCreation()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        builder.AddUnit(P1, UnitType.Settler, 3, 3);
        var state = builder.Build();
        var reversed = state with { Units = [.. state.Units.Reverse()], Cities = [.. state.Cities.Reverse()] };

        var view = GameEngine.ViewFor(reversed, P1);

        view.Units.Select(static unit => unit.Id.Value).ShouldBeInOrder();
        view.Cities.Select(static city => city.Id.Value).ShouldBeInOrder();
        view.Version.ShouldBe(GameEngine.ViewFor(state, P1).Version);
    }

    [Fact]
    public void ViewFor_EnemyInSight_ShowsTheirUnit()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, UnitType.Soldier, 2, 2);
        var enemy = builder.AddUnit(P2, UnitType.Soldier, 4, 2);

        var view = GameEngine.ViewFor(builder.Build(), P1);

        view.Units.ShouldContain(unit => unit.Id == enemy);
    }

    [Fact]
    public void ViewFor_Tiles_RevealTerrainOnlyForSeenTiles()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, UnitType.Soldier, 2, 2);

        var view = GameEngine.ViewFor(builder.Build(), P1);

        view.Tiles.Length.ShouldBe(Width * Height);
        view.Tiles.Single(tile => tile.Coord == At(2, 2)).ShouldBe(new TileView(At(2, 2), TileVisibility.Visible, TerrainType.Plains));
        view.Tiles.Single(tile => tile.Coord == At(9, 5)).ShouldBe(new TileView(At(9, 5), TileVisibility.Unexplored, null));
    }

    [Fact]
    public void ViewFor_UnitMovedAway_KeepsLeftTilesAsExplored()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, UnitType.Soldier, 4, 4);
        var behind = At(2, 4);

        var moved = GameEngine.Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(6, 4))).ShouldSucceed().State;
        var view = GameEngine.ViewFor(moved, P1);

        view.Tiles.Single(tile => tile.Coord == behind).ShouldBe(new TileView(behind, TileVisibility.Explored, TerrainType.Plains));
        moved.FindPlayer(P1)!.Explored.ShouldContain(behind);
    }

    [Fact]
    public void ViewFor_Always_ListsPlayersWithoutTheirExploration()
    {
        var view = GameEngine.ViewFor(new GameStateBuilder().WithCapitals().Build(), P2);

        view.Viewer.ShouldBe(P2);
        view.Players.ShouldBe([new PlayerSummary(P1, PlayerStatus.Active), new PlayerSummary(P2, PlayerStatus.Active)]);
    }

    [Fact]
    public void ViewFor_ViewerOutsideTheGame_Throws()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        Should.Throw<ArgumentException>(() => GameEngine.ViewFor(state, new PlayerId(Guid.NewGuid())));
    }

    [Fact]
    public void UpdateExplored_InactivePlayer_IsNotUpdated()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var state = builder.Build();
        var resigned = state with
        {
            Players = [state.Players[0], state.Players[1] with { Status = PlayerStatus.Resigned, Explored = [] }],
        };

        VisibilityCalculator.UpdateExplored(resigned).Players[1].Explored.ShouldBeEmpty();
    }
}
