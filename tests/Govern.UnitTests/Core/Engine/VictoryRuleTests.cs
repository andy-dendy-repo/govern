using System.Text.Json.Nodes;
using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

public class VictoryRuleTests
{
    [Fact]
    public void Execute_LastSettlerDestroyed_EliminatesPlayerAndFinishesGame()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Settler, 4, 3);
        var escort = builder.AddUnit(P2, TestRulesets.Soldier, 9, 7);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.Finished);
        update.State.Winner.ShouldBe(P1);
        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Eliminated);
        update.State.FindUnit(escort).ShouldBeNull();
        update.Events.ShouldContain(new PlayerEliminatedEvent(P2));
        update.Events.ShouldContain(new UnitDestroyedEvent(escort, P2, TestRulesets.Soldier, At(9, 7)));
        update.Events[^1].ShouldBe(new GameWonEvent(P1));
    }

    [Fact]
    public void Execute_LastCityCaptured_FinishesGame()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddCity(P2, 4, 3);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.Winner.ShouldBe(P1);
        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Eliminated);
    }

    [Fact]
    public void Execute_PlayerWithCityButNoUnits_StaysActive()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(TestRulesets.Default, state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.Players.ShouldAllBe(player => player.Status == PlayerStatus.Active);
    }

    [Fact]
    public void Execute_ResignOutOfTurn_OpponentWinsAndResignedUnitsAreRemoved()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var resigned = builder.AddUnit(P2, TestRulesets.Soldier, 8, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P2, new ResignCommand()).ShouldSucceed();

        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Resigned);
        update.State.FindUnit(resigned).ShouldBeNull();
        update.State.Status.ShouldBe(GameStatus.Finished);
        update.State.Winner.ShouldBe(P1);
        update.Events[0].ShouldBe(new PlayerResignedEvent(P2));
    }

    [Fact]
    public void Execute_AfterGameFinished_RejectsEveryCommand()
    {
        var state = new GameStateBuilder().WithCapitals().Build();
        var finished = GameEngine.Execute(TestRulesets.Default, state, P2, new ResignCommand()).ShouldSucceed().State;

        GameEngine.Execute(TestRulesets.Default, finished, P1, new EndTurnCommand()).ShouldFailWith(RuleViolations.GameFinished);
        GameEngine.Execute(TestRulesets.Default, finished, P2, new ResignCommand()).ShouldFailWith(RuleViolations.GameFinished);
    }

    [Fact]
    public void Execute_LastUnitCanFoundCitiesByRuleset_KeepsThePlayerInTheGame()
    {
        var ruleset = TestRulesets.With(json => json["units"]![1]!["abilities"] = new JsonArray("attack", "build_bridge", "found_city"));
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Settler, 4, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 9, 7);

        var update = GameEngine.Execute(ruleset, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Active);
    }

    [Fact]
    public void Execute_CurrentPlayerResignsWithThreePlayers_TheGameGoesOnWithTheNextPlayer()
    {
        var builder = ThreePlayers();
        var resigned = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var next = builder.AddUnit(P2, TestRulesets.Soldier, 8, 5, movementLeft: 0);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ResignCommand()).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.Winner.ShouldBeNull();
        update.State.CurrentPlayer.ShouldBe(P2);
        update.State.Turn.ShouldBe(1);
        update.State.FindUnit(resigned).ShouldBeNull();
        update.State.FindUnit(next)!.MovementLeft.ShouldBe(TestRulesets.Default.Unit(TestRulesets.Soldier).Movement);
        update.Events.ShouldContain(new TurnEndedEvent(P1, P2, 1));
    }

    [Fact]
    public void Execute_ResignOutOfTurnWithThreePlayers_KeepsTheTurnAndTheResignedCities()
    {
        var builder = ThreePlayers();
        var city = builder.AddCity(P3, 5, 5);

        var update = GameEngine.Execute(TestRulesets.Default, builder.Build(), P3, new ResignCommand()).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.CurrentPlayer.ShouldBe(P1);
        update.State.FindPlayer(P3)!.Status.ShouldBe(PlayerStatus.Resigned);
        update.State.FindCity(city)!.Owner.ShouldBe(P3);
        update.Events.OfType<TurnEndedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Execute_ResignedPlayersUndefendedCity_CanBeCaptured()
    {
        var builder = ThreePlayers();
        var city = builder.AddCity(P3, 5, 5);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 4, 5);
        var resigned = GameEngine.Execute(TestRulesets.Default, builder.Build(), P3, new ResignCommand()).ShouldSucceed().State;

        var update = GameEngine.Execute(TestRulesets.Default, resigned, P1, new AttackCommand(attacker, At(5, 5))).ShouldSucceed();

        update.State.FindCity(city)!.Owner.ShouldBe(P1);
        update.State.Status.ShouldBe(GameStatus.InProgress);
    }

    [Fact]
    public void Execute_OneOfThreeEliminated_TheGameGoesOnUntilOneIsLeft()
    {
        var builder = new GameStateBuilder().WithPlayers(P1, P2, P3).WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 5);
        builder.AddCity(P3, 4, 5);
        var state = builder.Build();

        var first = GameEngine.Execute(TestRulesets.Default, state, P1, new AttackCommand(attacker, At(4, 5))).ShouldSucceed();
        var last = GameEngine.Execute(TestRulesets.Default, first.State, P2, new ResignCommand()).ShouldSucceed();

        first.State.FindPlayer(P3)!.Status.ShouldBe(PlayerStatus.Eliminated);
        first.State.Status.ShouldBe(GameStatus.InProgress);
        last.State.Status.ShouldBe(GameStatus.Finished);
        last.State.Winner.ShouldBe(P1);
    }

    // Three players, each with a capital far from the others (P3's in the middle of the bottom edge).
    private static GameStateBuilder ThreePlayers()
    {
        var builder = new GameStateBuilder().WithPlayers(P1, P2, P3).WithCapitals();
        builder.AddCity(P3, 5, Height - 1);
        return builder;
    }
}
