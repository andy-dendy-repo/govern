using Govern.Core.Commands;
using Govern.Core.Engine;
using Govern.Core.Errors;
using Govern.Core.Events;
using Govern.Core.Players;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Rules;

public class VictoryRuleTests
{
    [Fact]
    public void Execute_LastSettlerDestroyed_EliminatesPlayerAndFinishesGame()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, UnitType.Soldier, 3, 3);
        builder.AddUnit(P2, UnitType.Settler, 4, 3);
        var escort = builder.AddUnit(P2, UnitType.Soldier, 9, 7);

        var update = GameEngine.Execute(builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.Finished);
        update.State.Winner.ShouldBe(P1);
        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Eliminated);
        update.State.FindUnit(escort).ShouldBeNull();
        update.Events.ShouldContain(new PlayerEliminatedEvent(P2));
        update.Events.ShouldContain(new UnitDestroyedEvent(escort, P2, At(9, 7)));
        update.Events[^1].ShouldBe(new GameWonEvent(P1));
    }

    [Fact]
    public void Execute_LastCityCaptured_FinishesGame()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, UnitType.Soldier, 3, 3);
        builder.AddCity(P2, 4, 3);

        var update = GameEngine.Execute(builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed();

        update.State.Winner.ShouldBe(P1);
        update.State.FindPlayer(P2)!.Status.ShouldBe(PlayerStatus.Eliminated);
    }

    [Fact]
    public void Execute_PlayerWithCityButNoUnits_StaysActive()
    {
        var state = new GameStateBuilder().WithCapitals().Build();

        var update = GameEngine.Execute(state, P1, new EndTurnCommand()).ShouldSucceed();

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.Players.ShouldAllBe(player => player.Status == PlayerStatus.Active);
    }

    [Fact]
    public void Execute_ResignOutOfTurn_OpponentWinsAndResignedUnitsAreRemoved()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var resigned = builder.AddUnit(P2, UnitType.Soldier, 8, 5);

        var update = GameEngine.Execute(builder.Build(), P2, new ResignCommand()).ShouldSucceed();

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
        var finished = GameEngine.Execute(state, P2, new ResignCommand()).ShouldSucceed().State;

        GameEngine.Execute(finished, P1, new EndTurnCommand()).ShouldFailWith(GameErrors.GameFinished);
        GameEngine.Execute(finished, P2, new ResignCommand()).ShouldFailWith(GameErrors.GameFinished);
    }
}
