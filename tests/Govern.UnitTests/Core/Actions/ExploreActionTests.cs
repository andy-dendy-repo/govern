using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Actions;

public class ExploreActionTests
{
    [Fact]
    public void Execute_UnexploredLandAround_WalksNowTowardsItAndKeepsExploring()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = builder.Build();

        var update = Execute(state, P1, new ExploreCommand(soldier));

        var unit = update.State.FindUnit(soldier)!;
        unit.Exploring.ShouldBeTrue();
        unit.Destination.ShouldBeNull();
        unit.MovementLeft.ShouldBe(0);
        update.Events.ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>().Path.Length.ShouldBe(2);
        Explored(update.State).ShouldBeGreaterThan(Explored(state));
    }

    [Fact]
    public void EndTurn_ExploringUnit_WalksOnAtTheStartOfItsOwnersTurn()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = Execute(builder.Build(), P1, new ExploreCommand(soldier)).State;

        var round2 = Execute(Execute(state, P1, new EndTurnCommand()).State, P2, new EndTurnCommand());

        round2.State.FindUnit(soldier)!.Exploring.ShouldBeTrue();
        round2.Events.OfType<UnitMovedEvent>().ShouldHaveSingleItem().UnitId.ShouldBe(soldier);
        Explored(round2.State).ShouldBeGreaterThan(Explored(state));
    }

    [Fact]
    public void EndTurn_NothingLeftToExplore_EndsExploring()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var state = Forget(builder.Build(), P1, At(8, 2));
        state = Execute(state, P1, new ExploreCommand(soldier)).State;

        for (var round = 0; round < 4; round++)
        {
            state = Execute(Execute(state, P1, new EndTurnCommand()).State, P2, new EndTurnCommand()).State;
        }

        state.FindUnit(soldier)!.Exploring.ShouldBeFalse();
        state.Players.Single(player => player.Id == P1).Explored.ShouldContain(At(8, 2));
    }

    [Fact]
    public void EndTurn_EnemyComesIntoSight_EndsExploringWithTheReason()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var scout = builder.AddUnit(P1, TestRulesets.Scout, 2, 2);
        var state = Execute(builder.Build(), P1, new ExploreCommand(scout)).State;
        var events = new List<GameEvent>();

        for (var round = 0; round < 8 && state.FindUnit(scout)!.Exploring; round++)
        {
            var afterP1 = Execute(state, P1, new EndTurnCommand());
            var afterP2 = Execute(afterP1.State, P2, new EndTurnCommand());
            events.AddRange(afterP2.Events);
            state = afterP2.State;
        }

        state.FindUnit(scout)!.Exploring.ShouldBeFalse();
        events.OfType<UnitOrderEndedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(OrderEndReason.EnemySighted);
    }

    [Fact]
    public void Execute_MapAllKnown_FailsWithNothingToExplore()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ExploreCommand(soldier)).ShouldFailWith(RuleViolations.NothingToExplore);
    }

    [Fact]
    public void Execute_UnitWithAJob_FailsWithUnitBusy()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var worker = builder.AddUnit(P1, TestRulesets.Builder, 2, 2, job: new UnitJob(1, 1, Improvement: TestRulesets.Farm));

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ExploreCommand(worker)).ShouldFailWith(RuleViolations.UnitBusy);
    }

    [Fact]
    public void Execute_EnemyUnit_FailsWithUnitNotFound()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 9, 7);

        GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new ExploreCommand(enemy)).ShouldFailWith(RuleViolations.UnitNotFound);
    }

    [Fact]
    public void Execute_AnotherOrder_StopsExploring()
    {
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1);
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, exploring: true);

        var moved = Execute(builder.Build(), P1, new MoveUnitCommand(soldier, At(3, 2)));
        var sent = Execute(builder.Build(), P1, new SetDestinationCommand(soldier, At(6, 2)));

        moved.State.FindUnit(soldier)!.Exploring.ShouldBeFalse();
        sent.State.FindUnit(soldier)!.Exploring.ShouldBeFalse();
    }

    public static TheoryData<string> PreviewScenarios =>
    [
        "unknown_map", "known_map", "working", "no_movement", "one_gap", "hidden_enemies_round_the_gap",
        "own_units_round_the_gap", "gap_behind_a_deep_river", "gap_in_a_lake",
    ];

    [Theory]
    [MemberData(nameof(PreviewScenarios))]
    public void CheckExplore_View_MatchesExecute(string scenario)
    {
        var (state, unit) = PreviewScenario(scenario);

        var preview = GameEngine.CheckExplore(TestRulesets.Default, GameEngine.ViewFor(TestRulesets.Default, state, P1), unit);
        var result = GameEngine.Execute(TestRulesets.Default, state, P1, new ExploreCommand(unit));

        preview.ShouldBe(result.IsSuccess ? null : result.Violation);
    }

    [Fact]
    public void Execute_NextStepTooDearForTheMovementLeft_KeepsExploringForNextTurn()
    {
        // Mountains all round but for a forest, which costs 2; the only unexplored tile lies past it.
        var walls = At(2, 2).Neighbors().Where(static tile => tile != At(3, 2)).Select(static tile => (tile.Column, tile.Row)).ToArray();
        var builder = new GameStateBuilder().WithCapitals().WithMapExplored(P1).WithTerrain(TestRulesets.Mountains, walls).WithTerrain(TestRulesets.Forest, (3, 2));
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: 1);
        var state = Forget(builder.Build(), P1, At(5, 2));

        var update = Execute(state, P1, new ExploreCommand(soldier));

        var unit = update.State.FindUnit(soldier)!;
        unit.Exploring.ShouldBeTrue();
        unit.Position.ShouldBe(At(2, 2));
        update.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ViewFor_Exploring_ShownOnlyToTheOwner()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 10, 8, exploring: true);
        var state = builder.Build();

        GameEngine.ViewFor(TestRulesets.Default, state, P1).FindOwnUnit(soldier)!.Exploring.ShouldBeTrue();
        GameEngine.ViewFor(TestRulesets.Default, state, P2).Units.Single(unit => unit.Id == soldier).Exploring.ShouldBeFalse();
    }

    [Fact]
    public void Serialize_ExploringUnit_RoundTrips()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, exploring: true);

        var restored = GameSerializer.DeserializeState(GameSerializer.Serialize(builder.Build()));

        restored.FindUnit(soldier)!.Exploring.ShouldBeTrue();
    }

    // The preview scenarios: a soldier at (2, 2) with what its owner knows around a forgotten gap at (8, 2), far out of its sight.
    private static (GameState State, Govern.Core.Units.UnitId Unit) PreviewScenario(string scenario)
    {
        var builder = new GameStateBuilder().WithCapitals();
        if (scenario != "unknown_map")
        {
            builder.WithMapExplored(P1);
        }

        var gap = At(8, 2);
        var around = gap.Neighbors().Where(static tile => tile.Column is >= 0 and < Width && tile.Row is >= 0 and < Height).ToList();
        var unit = builder.AddUnit(P1, TestRulesets.Soldier, 2, 2, movementLeft: scenario == "no_movement" ? 0 : null,
            job: scenario == "working" ? new UnitJob(1, 1, Improvement: TestRulesets.Farm) : null);
        switch (scenario)
        {
            case "hidden_enemies_round_the_gap":
                around.ForEach(tile => builder.AddUnit(P2, TestRulesets.Soldier, tile.Column, tile.Row));
                break;
            case "own_units_round_the_gap":
                around.ForEach(tile => builder.AddUnit(P1, TestRulesets.Soldier, tile.Column, tile.Row));
                break;
            case "gap_behind_a_deep_river":
                builder.WithRiver(TestRulesets.DeepRiver, [.. around.SelectMany(tile => tile.Neighbors()
                    .Where(other => other != gap && !around.Contains(other) && other.Column is >= 0 and < Width && other.Row is >= 0 and < Height)
                    .Select(other => ((tile.Column, tile.Row), (other.Column, other.Row))))]);
                break;
            case "gap_in_a_lake":
                builder.WithTerrain(TestRulesets.Lake, [.. around.Select(static tile => (tile.Column, tile.Row))]);
                break;
        }

        var state = builder.Build();
        return (scenario is "known_map" or "unknown_map" or "working" or "no_movement" ? state : Forget(state, P1, gap), unit);
    }

    private static int Explored(GameState state) => state.Players.Single(player => player.Id == P1).Explored.Length;

    // The player forgets a tile, as if they had never seen it.
    private static GameState Forget(GameState state, PlayerId player, Govern.Core.Map.HexCoord tile) => state with
    {
        Players = [.. state.Players.Select(candidate => candidate.Id == player ? candidate with { Explored = [.. candidate.Explored.Where(coord => coord != tile)] } : candidate)],
    };

    private static GameUpdate Execute(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).ShouldSucceed();
}
