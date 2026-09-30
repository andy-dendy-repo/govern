using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// The view-based previews must give exactly the answer Execute gives on the full state, for every tile near the unit.
public class GameEnginePreviewTests
{
    public static TheoryData<string> Scenarios => new(
        "open field",
        "own unit blocks",
        "own city in reach",
        "enemies just out of sight",
        "enemies adjacent",
        "defended enemy city",
        "map edge",
        "partial movement",
        "no movement",
        "settler without movement",
        "settler far from cities",
        "settler near own city",
        "settler on a city",
        "settler near enemy city",
        "not your turn",
        "enemy unit id",
        "game finished",
        "forest around",
        "partial movement facing forest",
        "mountains and lake around",
        "path around a lake",
        "on hills with enemies further out");

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ReachableTiles_View_MatchesMoveCommands(string scenario)
    {
        var (state, unitId, origin) = Build(scenario);

        var reachable = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        foreach (var target in origin.WithinRadius(3))
        {
            var accepted = GameEngine.Execute(TestRulesets.Default, state, P1, new MoveUnitCommand(unitId, target)).IsSuccess;
            reachable.Contains(target).ShouldBe(accepted, $"move to {target}");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void AttackTargets_View_MatchesAttackCommands(string scenario)
    {
        var (state, unitId, origin) = Build(scenario);

        var targets = GameEngine.AttackTargets(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        foreach (var target in origin.WithinRadius(3))
        {
            var accepted = GameEngine.Execute(TestRulesets.Default, state, P1, new AttackCommand(unitId, target)).IsSuccess;
            targets.Contains(target).ShouldBe(accepted, $"attack on {target}");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void CheckFoundCity_View_MatchesFoundCityCommand(string scenario)
    {
        var (state, unitId, _) = Build(scenario);

        var preview = GameEngine.CheckFoundCity(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        preview.ShouldBe(GameEngine.Execute(TestRulesets.Default, state, P1, new FoundCityCommand(unitId)).Violation);
    }

    // The state overload doesn't check the turn, so compare only where the viewer may act.
    public static TheoryData<string> ScenariosOnYourTurn =>
        new(Scenarios.Select(static row => row.Data).Where(static name => name is not ("not your turn" or "game finished")));

    [Theory]
    [MemberData(nameof(ScenariosOnYourTurn))]
    public void ReachableTiles_ViewOnYourTurn_EqualsTheStateResult(string scenario)
    {
        var (state, unitId, _) = Build(scenario);

        var fromView = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        fromView.ShouldBe(GameEngine.ReachableTiles(TestRulesets.Default, state, P1, unitId));
    }

    [Fact]
    public void AttackTargets_EnemyUnitAndCityAdjacent_ListsBothInRowOrder()
    {
        var (state, unitId, _) = Build("enemies adjacent");

        var targets = GameEngine.AttackTargets(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        targets.ShouldBe([At(5, 4), At(6, 5)]);
    }

    [Fact]
    public void ReachableTiles_OpenField_ListsEveryTileWithinFullMovement()
    {
        var (state, unitId, origin) = Build("open field");

        var reachable = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        reachable.ShouldBe([.. origin.WithinRadius(2).Where(coord => coord != origin).OrderBy(static c => c.Row).ThenBy(static c => c.Column)]);
    }

    [Theory]
    [InlineData("settler far from cities", null)]
    [InlineData("settler near own city", "city.too_close")]
    [InlineData("settler near enemy city", "city.too_close")]
    [InlineData("settler on a city", "city.tile_taken")]
    [InlineData("settler without movement", "unit.no_movement")]
    [InlineData("open field", "unit.cannot_found_city")]
    [InlineData("not your turn", "game.not_your_turn")]
    [InlineData("enemy unit id", "unit.not_found")]
    [InlineData("game finished", "game.finished")]
    public void CheckFoundCity_Scenario_GivesTheExpectedCode(string scenario, string? code)
    {
        var (state, unitId, _) = Build(scenario);

        (GameEngine.CheckFoundCity(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId)?.Code).ShouldBe(code);
    }

    [Fact]
    public void Previews_OwnCityInReach_CanBeEnteredButNotAttacked()
    {
        var (state, unitId, _) = Build("own city in reach");
        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        GameEngine.ReachableTiles(TestRulesets.Default, view, unitId).ShouldContain(At(0, 0));
        GameEngine.AttackTargets(TestRulesets.Default, view, unitId).ShouldBeEmpty();
    }

    [Fact]
    public void Previews_EnemiesJustOutOfSight_AreNotInTheView()
    {
        var (state, _, origin) = Build("enemies just out of sight");

        var view = GameEngine.ViewFor(TestRulesets.Default, state, P1);

        state.Units.Where(unit => unit.Owner == P2).ShouldAllBe(unit => unit.Position.DistanceTo(origin) == 3);
        view.Units.ShouldNotContain(unit => unit.Owner == P2);
        view.Cities.ShouldNotContain(city => city.Position == At(5, 2));
    }

    [Fact]
    public void ReachableTiles_ForestAround_EntersForestButNotBeyondIt()
    {
        var (state, unitId, _) = Build("forest around");

        var reachable = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        reachable.ShouldContain(At(6, 5));
        reachable.ShouldNotContain(At(7, 5));
        reachable.ShouldContain(At(6, 3));
    }

    [Fact]
    public void ReachableTiles_PartialMovementFacingForest_LeavesOutTheForest()
    {
        var (state, unitId, _) = Build("partial movement facing forest");

        var reachable = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        reachable.ShouldBe([At(6, 4), At(5, 6)]);
    }

    [Fact]
    public void ReachableTiles_MountainsAndLake_AreNeverReachable()
    {
        var (state, unitId, _) = Build("mountains and lake around");

        var reachable = GameEngine.ReachableTiles(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unitId);

        reachable.ShouldNotContain(At(6, 5));
        reachable.ShouldNotContain(At(5, 4));
        reachable.ShouldNotContain(At(4, 5));
        reachable.ShouldNotContain(At(5, 6));
        reachable.ShouldContain(At(6, 4));
        reachable.ShouldContain(At(6, 6));
    }

    [Fact]
    public void Previews_OnHills_ShowTheEnemyThreeTilesAway()
    {
        var (state, _, _) = Build("on hills with enemies further out");

        GameEngine.ViewFor(TestRulesets.Default, state, P1).Units.ShouldContain(unit => unit.Owner == P2);
    }

    [Fact]
    public void Previews_NullView_Throw()
    {
        Should.Throw<ArgumentNullException>(() => GameEngine.ReachableTiles(TestRulesets.Default, null!, new UnitId(1)));
        Should.Throw<ArgumentNullException>(() => GameEngine.AttackTargets(TestRulesets.Default, null!, new UnitId(1)));
        Should.Throw<ArgumentNullException>(() => GameEngine.CheckFoundCity(TestRulesets.Default, null!, new UnitId(1)));
    }

    private static (GameState State, UnitId Unit, HexCoord Origin) Build(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        var (unit, origin) = scenario switch
        {
            "own unit blocks" => (Soldier(builder, 5, 5), Blocker(builder, P1, 6, 5)),
            "enemies adjacent" => (Soldier(builder, 5, 5), EnemyCity(builder, 5, 4, Blocker(builder, P2, 6, 5))),
            "defended enemy city" => (Soldier(builder, 5, 5), EnemyCity(builder, 6, 5, Blocker(builder, P2, 6, 5))),
            // Next to the own capital at (0,0): it may pass through its own city and can't attack it.
            "own city in reach" => (Soldier(builder, 0, 1), At(0, 1)),
            // Distance 3 is outside sight 2 but inside the radius-3 probe, so the view and the state really differ there.
            "enemies just out of sight" => (Soldier(builder, 5, 5), EnemyCity(builder, 5, 2, Blocker(builder, P2, 8, 5))),
            "map edge" => (Soldier(builder, 0, 5), At(0, 5)),
            "partial movement" => (builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, movementLeft: 1), At(5, 5)),
            "no movement" => (builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, movementLeft: 0), At(5, 5)),
            "settler far from cities" => (builder.AddUnit(P1, TestRulesets.Settler, 5, 5), At(5, 5)),
            "settler without movement" => (builder.AddUnit(P1, TestRulesets.Settler, 5, 5, movementLeft: 0), At(5, 5)),
            "settler near own city" => (builder.AddUnit(P1, TestRulesets.Settler, 1, 1), At(1, 1)),
            "settler on a city" => (builder.AddUnit(P1, TestRulesets.Settler, 0, 0), At(0, 0)),
            "settler near enemy city" => (builder.AddUnit(P1, TestRulesets.Settler, 5, 5), EnemyCity(builder, 7, 5, At(5, 5))),
            "enemy unit id" => (Soldier(builder, 5, 5, P2), At(5, 5)),
            "partial movement facing forest" => (builder.AddUnit(P1, TestRulesets.Soldier, 5, 5, movementLeft: 1), At(5, 5)),
            // Sight 2 plus 1 on hills: the enemy at distance 3 is in the view, but out of reach.
            "on hills with enemies further out" => (Soldier(builder, 5, 5), Blocker(builder, P2, 8, 5)),
            _ => (Soldier(builder, 5, 5), At(5, 5)),
        };

        if (scenario == "enemy unit id")
        {
            Soldier(builder, 3, 5);
        }

        AddTerrain(builder, scenario);

        var state = (scenario == "not your turn" ? builder.WithCurrentPlayer(P2) : builder).Build();
        state = scenario == "game finished" ? state with { Status = GameStatus.Finished, Winner = P1 } : state;
        return (state, unit, origin);
    }

    // Terrain costs and blockers around the unit at (5, 5); the other scenarios play on plains only.
    private static void AddTerrain(GameStateBuilder builder, string scenario)
    {
        _ = scenario switch
        {
            "forest around" or "partial movement facing forest" => builder.WithTerrain(TestRulesets.Forest, (6, 5), (4, 5), (5, 4), (6, 6), (7, 5)),
            "mountains and lake around" => builder
                .WithTerrain(TestRulesets.Mountains, (6, 5), (5, 4))
                .WithTerrain(TestRulesets.Lake, (4, 5), (5, 6))
                .WithTerrain(TestRulesets.Hills, (6, 4)),
            // The lake blocks the straight line; the tile behind it costs 3 around it, one more than a full move.
            "path around a lake" => builder.WithTerrain(TestRulesets.Lake, (6, 5), (6, 4), (6, 6)),
            "on hills with enemies further out" => builder.WithTerrain(TestRulesets.Hills, (5, 5)),
            _ => builder,
        };
    }

    private static UnitId Soldier(GameStateBuilder builder, int column, int row, PlayerId? owner = null) =>
        builder.AddUnit(owner ?? P1, TestRulesets.Soldier, column, row);

    // Adds a unit that only serves as an obstacle or target; returns the tested unit's origin for chaining.
    private static HexCoord Blocker(GameStateBuilder builder, PlayerId owner, int column, int row)
    {
        builder.AddUnit(owner, TestRulesets.Soldier, column, row);
        return At(5, 5);
    }

    private static HexCoord EnemyCity(GameStateBuilder builder, int column, int row, HexCoord origin)
    {
        builder.AddCity(P2, column, row);
        return origin;
    }
}
