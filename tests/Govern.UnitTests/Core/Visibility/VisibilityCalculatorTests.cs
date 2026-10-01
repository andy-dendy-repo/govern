using Govern.Core.Actions;
using Govern.Core.Economy;
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
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P2, TestRulesets.Soldier, 9, 7);

        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        view.Units.ShouldHaveSingleItem().Id.ShouldBe(own);
        view.Cities.ShouldAllBe(city => city.Owner == P1);
    }

    [Fact]
    public void ViewFor_VisibleEnemy_ShowsItsHealth()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, damage: 10);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 5, 4, damage: 35);

        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        view.Units.Single(unit => unit.Id == own).Health.ShouldBe(90);
        view.Units.Single(unit => unit.Id == enemy).Health.ShouldBe(65);
    }

    [Fact]
    public void ViewFor_SameState_GivesTheSameNonNegativeVersion()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var first = GameEngine.ViewFor(TestRulesets.Default, state, P1);
        var second = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        first.Version.ShouldBe(second.Version);
        first.Version.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ViewFor_ChangeTheViewerSees_ChangesTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build();

        var moved = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(own, At(3, 2))).ShouldSucceed().State;

        GameEngine.ViewFor(TestRulesets.Default, moved, P1).Version.ShouldNotBe(GameEngine.ViewFor(TestRulesets.Default, state, P1).Version);
    }

    [Fact]
    public void ViewFor_EnemyMoveOutOfSight_KeepsTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2);
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 9, 7);
        var state = builder.Build();

        var moved = GameEngine.Execute(TestRulesets.Default, state, P2, new MoveUnitCommand(enemy, At(8, 7))).ShouldSucceed().State;

        moved.Version.ShouldBe(state.Version + 1);
        GameEngine.ViewFor(TestRulesets.Default, moved, P1).Version.ShouldBe(GameEngine.ViewFor(TestRulesets.Default, state, P1).Version);
    }

    [Fact]
    public void ViewFor_UnitsAndCities_AreOrderedByIdNotByCreation()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        builder.AddUnit(P1, TestRulesets.Settler, 3, 3);
        var state = builder.Build();
        var reversed = state with { Units = [.. state.Units.Reverse()], Cities = [.. state.Cities.Reverse()] };

        var view = GameEngine.ViewFor(TestRulesets.Default, reversed, P1);

        view.Units.Select(static unit => unit.Id.Value).ShouldBeInOrder();
        view.Cities.Select(static city => city.Id.Value).ShouldBeInOrder();
        view.Version.ShouldBe(GameEngine.ViewFor(TestRulesets.Default, state, P1).Version);
    }

    [Fact]
    public void ViewFor_EnemyInSight_ShowsTheirUnit()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 4, 2);

        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        view.Units.ShouldContain(unit => unit.Id == enemy);
    }

    [Fact]
    public void ViewFor_Tiles_RevealTerrainOnlyForSeenTiles()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        view.Tiles.Length.ShouldBe(Width * Height);
        view.TileAt(At(2, 2)).ShouldBe(new TileView(TileVisibility.Visible, TestRulesets.Plains));
        view.TileAt(At(9, 5)).ShouldBe(new TileView(TileVisibility.Unexplored, null));
    }

    [Fact]
    public void ViewFor_Rivers_ShowOnExploredTilesOnly()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithRiver(TestRulesets.River, ((2, 2), (3, 2)))
            .WithRiver(TestRulesets.DeepRiver, ((9, 5), (10, 5)));
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        view.TileAt(At(2, 2)).Rivers.ShouldBe(new TileRivers(East: TestRulesets.River));
        view.RiverBetween(At(3, 2), At(2, 2)).ShouldBe(TestRulesets.River);
        view.TileAt(At(9, 5)).Rivers.ShouldBeNull();
        view.RiverBetween(At(9, 5), At(10, 5)).ShouldBeNull();
    }

    [Fact]
    public void ViewFor_ImprovementsAndBridges_ShowOnVisibleTilesOnly()
    {
        var builder = new GameStateBuilder().WithCapitals()
            .WithRiver(TestRulesets.River, ((2, 2), (3, 2)), ((6, 2), (7, 2)))
            .WithBridge(((2, 2), (3, 2)), ((6, 2), (7, 2)))
            .WithImprovement(TestRulesets.Farm, (2, 3), (7, 2));
        var scout = builder.AddUnit(P1, TestRulesets.Soldier, 5, 2);
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 3);
        var state = builder.Build();

        // The scout explored (7, 2) and walks away, so the tile stays explored but no longer visible.
        var moved = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(scout, At(3, 2))).ShouldSucceed().State;
        var view = GameEngine.ViewFor(TestRulesets.Default, moved, P1);

        view.TileAt(At(2, 3)).Improvement.ShouldBe(TestRulesets.Farm);
        view.BridgeBetween(At(3, 2), At(2, 2)).ShouldBeTrue();
        view.CrossedRiverBetween(At(3, 2), At(2, 2)).ShouldBeNull();
        view.TileAt(At(7, 2)).Visibility.ShouldBe(TileVisibility.Explored);
        view.TileAt(At(7, 2)).Improvement.ShouldBeNull();
        view.TileAt(At(6, 2)).Visibility.ShouldBe(TileVisibility.Explored);
        view.BridgeBetween(At(6, 2), At(7, 2)).ShouldBeFalse();
        view.RiverBetween(At(6, 2), At(7, 2)).ShouldBe(TestRulesets.River);
    }

    [Fact]
    public void ViewFor_JobsBuildingsAndStockpile_ShowOnlyTheViewersOwn()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.River, ((4, 4), (5, 4)));
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, job: UnitJob.ForBridge(At(5, 4), 1));
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 5, 4, job: UnitJob.ForBridge(At(4, 4), 1));
        var ownCity = builder.AddCity(P1, 3, 3, buildings: [TestRulesets.Campus]);
        var enemyCity = builder.AddCity(P2, 6, 4, producingBuilding: TestRulesets.Harbour, buildings: [TestRulesets.TradePost]);
        var state = builder.Build();
        state = state with
        {
            Players = [state.Players[0] with { Resources = [new(TestRulesets.Food, 3)] }, state.Players[1] with { Resources = [new(TestRulesets.Gold, 9)] }],
        };

        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        view.Units.Single(unit => unit.Id == own).Job.ShouldBe(UnitJob.ForBridge(At(5, 4), 1));
        view.Units.Single(unit => unit.Id == enemy).Job.ShouldBeNull();
        view.Cities.Single(city => city.Id == ownCity).BuildingList.ShouldBe([TestRulesets.Campus]);
        var seen = view.Cities.Single(city => city.Id == enemyCity);
        seen.Buildings.ShouldBeNull();
        seen.ProducingBuilding.ShouldBeNull();
        view.Resources.ShouldBe([new(TestRulesets.Food, 3)]);
    }

    [Fact]
    public void ViewFor_UnitMovedAway_KeepsLeftTilesAsExplored()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var behind = At(2, 4);

        var moved = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new MoveUnitCommand(soldier, At(6, 4))).ShouldSucceed().State;
        var view = GameEngine.ViewFor(TestRulesets.Default, moved, P1);

        view.TileAt(behind).ShouldBe(new TileView(TileVisibility.Explored, TestRulesets.Plains));
        moved.FindPlayer(P1)!.Explored.ShouldContain(behind);
    }

    [Fact]
    public void CoordAt_EveryIndex_MatchesTheMapTileAndTileAt()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        for (var index = 0; index < view.Tiles.Length; index++)
        {
            var coord = view.CoordAt(index);
            coord.ShouldBe(state.Map.Tiles[index].Coord);
            view.TileAt(coord).ShouldBeSameAs(view.Tiles[index]);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Width * Height)]
    public void CoordAt_IndexOutsideTheMap_Throws(int index)
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, new GameStateBuilder().WithCapitals().Build(), P1);

        Should.Throw<ArgumentOutOfRangeException>(() => view.CoordAt(index));
    }

    [Fact]
    public void TileAt_CoordOutsideTheMap_Throws()
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, new GameStateBuilder().WithCapitals().Build(), P1);

        view.Contains(At(Width, 0)).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => view.TileAt(At(Width, 0)));
    }

    [Fact]
    public void ViewFor_EnemyCityInSight_HidesItsProductionButShowsTheOwnersOwn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var enemyCity = builder.AddCity(P2, 6, 4, TestRulesets.Settler, progress: 3);
        var state = builder.Build();

        var seenByP1 = GameEngine.ViewFor(TestRulesets.Default, state, P1).Cities.Single(city => city.Id == enemyCity);
        var seenByP2 = GameEngine.ViewFor(TestRulesets.Default, state, P2).Cities.Single(city => city.Id == enemyCity);

        seenByP1.ShouldBe(new CityView(enemyCity, P2, $"City {enemyCity.Value}", At(6, 4), Producing: null, ProductionProgress: null));
        seenByP2.Producing.ShouldBe(TestRulesets.Settler);
        seenByP2.ProductionProgress.ShouldBe(3);
    }

    [Fact]
    public void ViewFor_EnemyProductionChangesInSight_KeepsTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var enemyCity = builder.AddCity(P2, 6, 4);
        var state = builder.Build();
        var city = state.FindCity(enemyCity)!;
        var progressed = state with
        {
            Cities = [.. state.Cities.Select(c => c.Id == enemyCity ? city with { Producing = TestRulesets.Settler, ProductionProgress = 5 } : c)],
        };
        GameEngine.ViewFor(TestRulesets.Default, state, P1).Cities.ShouldContain(c => c.Id == enemyCity, "the enemy city must be in sight for this test to mean anything");

        GameEngine.ViewFor(TestRulesets.Default, progressed, P1).Version.ShouldBe(GameEngine.ViewFor(TestRulesets.Default, state, P1).Version);
        GameEngine.ViewFor(TestRulesets.Default, progressed, P2).Version.ShouldNotBe(GameEngine.ViewFor(TestRulesets.Default, state, P2).Version);
    }

    [Fact]
    public void ViewFor_EnemyUnitInSight_HidesItsMovementButShowsTheOwnersOwn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4, movementLeft: 1);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 5, 4, movementLeft: 1);
        var state = builder.Build();

        var p1View = GameEngine.ViewFor(TestRulesets.Default, state, P1);
        var p2View = GameEngine.ViewFor(TestRulesets.Default, state, P2);

        p1View.Units.Single(unit => unit.Id == enemy).ShouldBe(new UnitView(enemy, P2, TestRulesets.Soldier, At(5, 4), MovementLeft: null, Health: 100));
        p1View.Units.Single(unit => unit.Id == own).MovementLeft.ShouldBe(1);
        p2View.Units.Single(unit => unit.Id == enemy).MovementLeft.ShouldBe(1);
        p2View.Units.Single(unit => unit.Id == own).MovementLeft.ShouldBeNull();
    }

    [Fact]
    public void ViewFor_EnemyMovementChangesInSight_KeepsTheVersion()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 6, 4, movementLeft: 2);
        var state = builder.Build();
        var spent = state with { Units = [.. state.Units.Select(unit => unit.Id == enemy ? unit with { MovementLeft = 0 } : unit)] };
        GameEngine.ViewFor(TestRulesets.Default, state, P1).Units.ShouldContain(unit => unit.Id == enemy, "the enemy unit must be in sight for this test to mean anything");

        GameEngine.ViewFor(TestRulesets.Default, spent, P1).Version.ShouldBe(GameEngine.ViewFor(TestRulesets.Default, state, P1).Version);
        GameEngine.ViewFor(TestRulesets.Default, spent, P2).Version.ShouldNotBe(GameEngine.ViewFor(TestRulesets.Default, state, P2).Version);
    }

    [Fact]
    public void ViewFor_Always_ListsPlayersWithoutTheirExploration()
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, new GameStateBuilder().WithCapitals().Build(), P2);

        view.Viewer.ShouldBe(P2);
        view.Players.ShouldBe([new PlayerSummary(P1, PlayerStatus.Active), new PlayerSummary(P2, PlayerStatus.Active)]);
    }

    [Fact]
    public void ViewFor_ViewerOutsideTheGame_Throws()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        Should.Throw<ArgumentException>(() => GameEngine.ViewFor(TestRulesets.Default, state, new PlayerId(Guid.NewGuid())));
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

        VisibilityCalculator.UpdateExplored(TestRulesets.Default, resigned).Players[1].Explored.ShouldBeEmpty();
    }

    [Fact]
    public void VisibleTiles_UnitOnHills_SeesOneTileFurther()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Hills, (5, 5));
        builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        var visible = VisibilityCalculator.VisibleTiles(TestRulesets.Default, builder.Build(), P1);

        visible.ShouldContain(At(8, 5));
        visible.ShouldNotContain(At(9, 5));
    }

    [Fact]
    public void VisibleTiles_CityOnHills_SeesOneTileFurther()
    {
        var builder = new GameStateBuilder().WithTerrain(TestRulesets.Hills, (5, 5));
        builder.AddCity(P1, 5, 5);
        builder.AddCity(P2, Width - 1, Height - 1);

        var visible = VisibilityCalculator.VisibleTiles(TestRulesets.Default, builder.Build(), P1);

        visible.ShouldContain(At(8, 5));
        visible.ShouldNotContain(At(9, 5));
    }

    [Fact]
    public void VisibleTiles_UnitNextToHills_KeepsItsOwnSight()
    {
        var builder = new GameStateBuilder().WithCapitals().WithTerrain(TestRulesets.Hills, (6, 5));
        builder.AddUnit(P1, TestRulesets.Soldier, 5, 5);

        VisibilityCalculator.VisibleTiles(TestRulesets.Default, builder.Build(), P1).ShouldNotContain(At(8, 5));
    }
}
