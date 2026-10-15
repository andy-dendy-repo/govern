using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Engine;

// Simultaneous turns (F9): everyone acts in the same round, and the round resolves once every active player has ended.
public class RoundRuleTests
{
    private static readonly int SoldierMovement = TestRulesets.Default.Unit(TestRulesets.Soldier).Movement;

    [Fact]
    public void Execute_Simultaneous_EveryPlayerActsInTheSameRound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var theirs = builder.AddUnit(P2, TestRulesets.Soldier, 8, 2);

        var first = Execute(Simultaneous(builder), P2, new MoveUnitCommand(theirs, At(7, 2)));
        var second = Execute(first.State, P1, new MoveUnitCommand(own, At(3, 2)));

        second.State.FindUnit(own)!.Position.ShouldBe(At(3, 2));
        second.State.FindUnit(theirs)!.Position.ShouldBe(At(7, 2));
        second.State.Turn.ShouldBe(1);
    }

    [Fact]
    public void Execute_SimultaneousEndTurn_MarksThePlayerAndKeepsTheRound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        var update = Execute(Simultaneous(builder), P1, new EndTurnCommand());

        update.State.FindPlayer(P1)!.EndedTurn.ShouldBeTrue();
        update.State.Turn.ShouldBe(1);
        update.Events.ShouldBe([new PlayerEndedTurnEvent(P1, Ended: true)]);
        GameEngine.Execute(TestRulesets.Default, update.State, P1, new MoveUnitCommand(soldier, At(3, 2)))
            .ShouldFailWith(RuleViolations.TurnEnded);
        GameEngine.Execute(TestRulesets.Default, update.State, P1, new EndTurnCommand()).ShouldFailWith(RuleViolations.TurnEnded);
    }

    [Fact]
    public void Execute_SimultaneousLastPlayerEnds_ResolvesTheRoundForEveryone()
    {
        var builder = new GameStateBuilder();
        var first = builder.AddCity(P1, 2, 2, progress: 1);
        var second = builder.AddCity(P2, 8, 8, progress: 1);
        var tired = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3, movementLeft: 0);
        var resting = builder.AddUnit(P2, TestRulesets.Soldier, 7, 7, movementLeft: 0);
        var ended = Execute(Simultaneous(builder), P2, new EndTurnCommand()).State;

        var update = Execute(ended, P1, new EndTurnCommand());

        update.State.Turn.ShouldBe(2);
        update.State.FindCity(first)!.ProductionProgress.ShouldBe(2);
        update.State.FindCity(second)!.ProductionProgress.ShouldBe(2);
        update.State.FindUnit(tired)!.MovementLeft.ShouldBe(SoldierMovement);
        update.State.FindUnit(resting)!.MovementLeft.ShouldBe(SoldierMovement);
        update.State.Players.ShouldAllBe(player => !player.EndedTurn);
        update.Events.ShouldContain(new RoundEndedEvent(2));
        update.Events.OfType<TurnEndedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Execute_SimultaneousTwoUnitsToOneTile_TheSecondIsRejected()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var own = builder.AddUnit(P1, TestRulesets.Soldier, 4, 2);
        var theirs = builder.AddUnit(P2, TestRulesets.Soldier, 6, 2);
        var first = Execute(Simultaneous(builder), P2, new MoveUnitCommand(theirs, At(5, 2))).State;

        var result = GameEngine.Execute(TestRulesets.Default, first, P1, new MoveUnitCommand(own, At(5, 2)));

        result.ShouldFailWith(RuleViolations.TileOccupied);
    }

    [Fact]
    public void Execute_SimultaneousLastPlayerStillPlayingResigns_ResolvesTheRound()
    {
        var builder = ThreePlayers();
        var state = Execute(Execute(Simultaneous(builder), P1, new EndTurnCommand()).State, P2, new EndTurnCommand()).State;

        var update = Execute(state, P3, new ResignCommand());

        update.State.Status.ShouldBe(GameStatus.InProgress);
        update.State.Turn.ShouldBe(2);
        update.Events.ShouldContain(new RoundEndedEvent(2));
    }

    [Fact]
    public void Execute_SimultaneousFirstSeatResigns_TheRoundGoesOn()
    {
        var builder = ThreePlayers();
        var ended = Execute(Simultaneous(builder), P3, new EndTurnCommand()).State;

        var update = Execute(ended, P1, new ResignCommand());

        update.State.CurrentPlayer.ShouldBe(P2);
        update.State.Turn.ShouldBe(1);
        update.State.FindPlayer(P3)!.EndedTurn.ShouldBeTrue();
        update.Events.OfType<TurnEndedEvent>().ShouldBeEmpty();
        update.Events.OfType<RoundEndedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void ViewFor_Simultaneous_ShowsWhoEndedAndWhoMayAct()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = Execute(Simultaneous(builder), P1, new EndTurnCommand()).State;

        var own = GameEngine.ViewFor(TestRulesets.Default, state, P1);
        var theirs = GameEngine.ViewFor(TestRulesets.Default, state, P2);

        own.TurnMode.ShouldBe(TurnMode.Simultaneous);
        (own.MayAct, own.EndedTurn).ShouldBe((false, true));
        (theirs.MayAct, theirs.EndedTurn).ShouldBe((true, false));
        theirs.Players.Single(player => player.Id == P1).EndedTurn.ShouldBeTrue();
        GameEngine.ReachableTiles(TestRulesets.Default, own, soldier).ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_Sequential_LeavesTheTurnModeOut_AndSimultaneousReadsBack()
    {
        var sequential = new GameStateBuilder().WithCapitals().Build();
        var simultaneous = Execute(Simultaneous(new GameStateBuilder().WithCapitals()), P1, new EndTurnCommand()).State;

        var json = GameSerializer.Serialize(simultaneous);

        GameSerializer.Serialize(sequential).ShouldNotContain("turnMode");
        GameSerializer.Serialize(sequential).ShouldNotContain("endedTurn");
        json.ShouldContain("\"turnMode\":\"simultaneous\"");
        GameSerializer.Serialize(GameSerializer.DeserializeState(json)).ShouldBe(json);
    }

    [Fact]
    public void Create_SimultaneousSetup_StartsASimultaneousGame()
    {
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 7, TurnMode: TurnMode.Simultaneous));

        state.TurnMode.ShouldBe(TurnMode.Simultaneous);
        state.MayAct(P1).ShouldBeTrue();
        state.MayAct(P2).ShouldBeTrue();
    }

    private static GameState Simultaneous(GameStateBuilder builder) => builder.Build() with { TurnMode = TurnMode.Simultaneous };

    private static GameUpdate Execute(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).ShouldSucceed();

    private static GameStateBuilder ThreePlayers()
    {
        var builder = new GameStateBuilder().WithPlayers(P1, P2, P3).WithCapitals();
        builder.AddCity(P3, 5, Height - 1);
        return builder;
    }
}
