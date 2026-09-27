using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Maps;
using Govern.Core.Players;
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
