using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Serialization;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.Science;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Visibility;

public class EventVisibilityTests
{
    [Fact]
    public void EventsFor_OwnMove_KeepsTheWholePath()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var soldier = builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var state = builder.Build();

        var update = Execute(state, P1, new MoveUnitCommand(soldier, At(6, 4)));

        var moved = VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P1).ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>();
        moved.Path.ShouldBe([At(5, 4), At(6, 4)]);
        moved.Owner.ShouldBe(P1);
        moved.UnitType.ShouldBe(TestRulesets.Soldier);
    }

    [Fact]
    public void EventsFor_EnemyMoveOutOfSight_IsDropped()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2);
        builder.AddUnit(P1, TestRulesets.Soldier, 2, 2);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 9, 5);
        var state = builder.Build();

        var update = Execute(state, P2, new MoveUnitCommand(enemy, At(8, 5)));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P1).ShouldBeEmpty();
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P2).ShouldHaveSingleItem();
    }

    [Fact]
    public void EventsFor_EnemyMoveIntoSight_KeepsOnlyTheSeenTiles()
    {
        var builder = new GameStateBuilder().WithCapitals().WithCurrentPlayer(P2);
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var enemy = builder.AddUnit(P2, TestRulesets.Soldier, 8, 4);
        var state = builder.Build();

        var update = Execute(state, P2, new MoveUnitCommand(enemy, At(6, 4)));

        var moved = VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P1).ShouldHaveSingleItem().ShouldBeOfType<UnitMovedEvent>();
        moved.Path.ShouldBe([At(6, 4)]);
    }

    [Fact]
    public void EventsFor_AttackOnYourUnit_IsSeenByBothPlayers()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);
        var state = builder.Build();

        var update = Execute(state, P1, new AttackCommand(attacker, At(4, 3)));

        foreach (var viewer in new[] { P1, P2 })
        {
            var attacked = VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, viewer).OfType<UnitAttackedEvent>().ShouldHaveSingleItem();
            attacked.AttackerId.ShouldBe(attacker);
            attacked.DefenderId.ShouldBe(defender);
            attacked.From.ShouldBe(At(3, 3));
        }
    }

    [Fact]
    public void EventsFor_AttackerOnAnUnseenTile_HidesItsIdAndTile()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var defender = builder.AddUnit(P2, TestRulesets.Soldier, 9, 5);
        var state = builder.Build();
        var hidden = new UnitAttackedEvent(new UnitId(999), P1, TestRulesets.Archer, At(2, 2), defender, P2, TestRulesets.Soldier, At(9, 5), 20, 0);

        var events = VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [hidden]), P2);

        var attacked = events.ShouldHaveSingleItem().ShouldBeOfType<UnitAttackedEvent>();
        attacked.AttackerId.ShouldBeNull();
        attacked.From.ShouldBeNull();
        attacked.AttackerType.ShouldBe(TestRulesets.Archer);
        attacked.DamageDealt.ShouldBe(20);
        var stored = GameSerializer.Deserialize<GameEvent>(GameSerializer.Serialize<GameEvent>(attacked)).ShouldBeOfType<UnitAttackedEvent>();
        (stored.AttackerId, stored.From, stored.DamageDealt).ShouldBe((null, null, 20));
    }

    [Fact]
    public void EventsFor_FightBetweenUnseenUnits_IsDropped()
    {
        var state = new GameStateBuilder().WithCapitals().Build();
        var fight = new UnitAttackedEvent(new UnitId(998), P1, TestRulesets.Soldier, At(6, 5), new UnitId(999), P1, TestRulesets.Soldier, At(7, 5), 20, 5);

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [fight]), P2).ShouldBeEmpty();
    }

    [Fact]
    public void EventsFor_ProductionChange_ReachesOnlyTheOwner()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 4, 4);
        builder.AddCity(P2, 5, 4);
        var state = builder.Build();

        var update = Execute(state, P1, new SetProductionCommand(city, TestRulesets.Settler));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P1).ShouldHaveSingleItem().ShouldBeOfType<ProductionChangedEvent>();
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P2).ShouldBeEmpty();
    }

    [Fact]
    public void EventsFor_EndTurn_GivesTheOpponentTheTurnButNotTheEndTurnUpkeep()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 2, 2, TestRulesets.Soldier, progress: TestRulesets.Default.Unit(TestRulesets.Soldier).ProductionCost - 1);
        builder.AddCity(P2, 9, 7);
        var state = builder.Build();

        var update = Execute(state, P1, new EndTurnCommand());

        update.Events.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().CityId.ShouldBe(city);
        var own = VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P1);
        own.OfType<UnitCreatedEvent>().ShouldHaveSingleItem();
        own.OfType<TurnEndedEvent>().ShouldHaveSingleItem();
        var theirs = VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P2);
        theirs.ShouldHaveSingleItem().ShouldBe(new TurnEndedEvent(P1, P2, 1));
    }

    [Fact]
    public void EventsFor_YourCityCaptured_IsSeenEvenFromAfar()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var city = builder.AddCity(P2, 5, 5);
        var state = builder.Build();
        var captured = new CityCapturedEvent(city, "City 2", At(5, 5), P2, P1, new UnitId(999));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [captured]), P2).ShouldHaveSingleItem();
    }

    [Fact]
    public void EventsFor_EnemyUnitCreatedAndTerritoryOnASeenTile_PassWithoutTheirCity()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P2, TestRulesets.Soldier, 5, 4);
        var state = builder.Build();
        var city = new CityId(77);
        GameEvent[] events = [new UnitCreatedEvent(new UnitId(78), P1, TestRulesets.Soldier, At(5, 5), city), new TerritoryGrewEvent(city, P1, At(6, 4))];

        var seen = VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [.. events]), P2);

        seen.OfType<UnitCreatedEvent>().ShouldHaveSingleItem().CityId.ShouldBeNull();
        seen.OfType<TerritoryGrewEvent>().ShouldHaveSingleItem().CityId.ShouldBeNull();
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [.. events]), P1).ShouldBe(events);
    }

    [Fact]
    public void EventsFor_BridgeSeenOnlyFromTheFarEnd_IsDropped()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P2, TestRulesets.Soldier, 5, 4);
        var state = builder.Build();
        var hidden = new BridgeBuiltEvent(new UnitId(79), P1, At(9, 4), At(7, 4));
        var seenFrom = new BridgeBuiltEvent(new UnitId(79), P1, At(6, 4), At(7, 4));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [hidden]), P2).ShouldBeEmpty();
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [seenFrom]), P2).ShouldBe([seenFrom]);
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void LogEvents_EveryTypeAndItsRedactedShape_ReadBackFromTheStoredJson(string type)
    {
        var (_, samples) = Samples();
        var sample = samples.Single(gameEvent => Discriminator(gameEvent.GetType()) == type);

        foreach (var stored in new[] { sample, Hidden(sample) })
        {
            var json = GameSerializer.Serialize(stored);
            GameSerializer.Serialize(GameSerializer.Deserialize<GameEvent>(json)).ShouldBe(json);
        }
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void EventsFor_EveryEventType_ReachesItsOwner(string type)
    {
        var (state, samples) = Samples();
        var sample = samples.Single(gameEvent => Discriminator(gameEvent.GetType()) == type);

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [sample]), P1).ShouldBe([sample]);
    }

    [Fact]
    public void EventsFor_PrivateEventsOfTheOpponent_NeverPass()
    {
        var (state, samples) = Samples();
        // The capture took P2's city, so P2 learns of it wherever it is.
        var everyone = new[] { typeof(TurnEndedEvent), typeof(PlayerEndedTurnEvent), typeof(RoundEndedEvent), typeof(PlayerResignedEvent), typeof(PlayerEliminatedEvent), typeof(GameWonEvent), typeof(CityCapturedEvent) };

        // The samples sit on P1's own tiles, out of P2's sight: only the game-wide events (and the lost city) reach P2.
        var seenByP2 = VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [.. samples]), P2);

        seenByP2.Select(static gameEvent => gameEvent.GetType()).ShouldBe(everyone, ignoreOrder: true);
    }

    [Fact]
    public void EventsFor_TradeEvents_ReachOnlyTheTwoSidesOfTheDeal()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var home = builder.AddCity(P1, 1, 1);
        var state = builder.Build();
        var host = state.Cities.First(city => city.Owner == P2).Id;
        GameEvent[] events =
        [
            new TradeDealChangedEvent(new TradeDealId(996), P1, P2, home, host, TradeChange.Proposed),
            new TradeExchangedEvent(new TradeDealId(996), P1, P2, home, host, [new ResourceAmount(TestRulesets.Food, 3)], [new ResourceAmount(TestRulesets.Stone, 1)], false, false),
        ];
        var own = new TradeDealChangedEvent(new TradeDealId(997), P1, P1, home, home, TradeChange.Proposed);

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [.. events]), P1).ShouldBe(events);
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [.. events]), P2).ShouldBe(events);
        VisibilityCalculator.EventsFor(TestRulesets.Default, state, new GameUpdate(state, [own]), P2).ShouldBeEmpty();
    }

    public static TheoryData<string> EventTypes() =>
        [.. typeof(GameEvent).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(static attribute => (string)attribute.TypeDiscriminator!)];

    // One event of every type, all about P1's units and city in the corner far from P2 (the capture took the city from P2).
    private static (GameState State, ImmutableArray<GameEvent> Samples) Samples()
    {
        var builder = new GameStateBuilder();
        var city = builder.AddCity(P1, 1, 1);
        var unit = builder.AddUnit(P1, TestRulesets.Builder, 2, 1);
        builder.AddCity(P2, 10, 8);
        var state = builder.Build();
        var job = new UnitJob(0, 1, Improvement: TestRulesets.Farm);

        ImmutableArray<GameEvent> samples =
        [
            new UnitMovedEvent(unit, P1, TestRulesets.Builder, [At(2, 1)]),
            new UnitOrderEndedEvent(unit, P1, TestRulesets.Builder, At(2, 1), At(2, 1), OrderEndReason.Arrived),
            new UnitCreatedEvent(unit, P1, TestRulesets.Builder, At(1, 1), city),
            new UnitDestroyedEvent(new UnitId(999), P1, TestRulesets.Soldier, At(1, 0)),
            new UnitAttackedEvent(unit, P1, TestRulesets.Builder, At(2, 1), new UnitId(997), P1, TestRulesets.Soldier, At(1, 2), 0, 0),
            new CityFoundedEvent(city, P1, At(1, 1), "City 1", new UnitId(998)),
            new CityCapturedEvent(city, "City 1", At(1, 1), P2, P1, unit),
            new ProductionChangedEvent(city, TestRulesets.Settler),
            new TurnEndedEvent(P1, P2, 1),
            new PlayerEndedTurnEvent(P1, Ended: true),
            new RoundEndedEvent(2),
            new BuildStartedEvent(unit, job),
            new ImprovementBuiltEvent(unit, P1, At(2, 1), TestRulesets.Farm),
            new BridgeBuiltEvent(unit, P1, At(2, 1), At(3, 1)),
            new BuildingCompletedEvent(city, TestRulesets.ArmyCamp, At(2, 1)),
            new TerritoryGrewEvent(city, P1, At(0, 1)),
            new UnitStarvedEvent(unit, 90),
            new BuildingDamagedEvent(city, TestRulesets.ArmyCamp, 90),
            new UpkeepPrioritySetEvent(city, UpkeepPriority.UnitsFirst),
            new TradeDealChangedEvent(new TradeDealId(996), P1, P1, city, new CityId(995), TradeChange.Accepted),
            new TradeExchangedEvent(new TradeDealId(996), P1, P1, city, new CityId(995), [new ResourceAmount(TestRulesets.Food, 3)], [new ResourceAmount(TestRulesets.Stone, 1)], false, true),
            new ScienceNodeChangedEvent(P1, "archery", ScienceNodeChange.Unlocked),
            new UnitUpgradedEvent(unit, P1, At(2, 1), TestRulesets.Soldier, TestRulesets.Swordsman),
            new BuildingUpgradedEvent(unit, P1, At(2, 1), TestRulesets.Campus, TestRulesets.University, city),
            new PlayerResignedEvent(P2),
            new PlayerEliminatedEvent(P2),
            new GameWonEvent(P1),
        ];

        return (state, samples);
    }

    // The redacted shapes the opponent can get: the attack from the fog, a new unit and grown territory without their city.
    private static GameEvent Hidden(GameEvent gameEvent) => gameEvent switch
    {
        UnitAttackedEvent attacked => attacked with { AttackerId = null, From = null },
        UnitCreatedEvent created => created with { CityId = null },
        TerritoryGrewEvent grew => grew with { CityId = null },
        BuildingUpgradedEvent upgraded => upgraded with { CityId = null },
        _ => gameEvent,
    };

    private static string Discriminator(Type type) =>
        (string)typeof(GameEvent).GetCustomAttributes<JsonDerivedTypeAttribute>().Single(attribute => attribute.DerivedType == type).TypeDiscriminator!;

    private static GameUpdate Execute(GameState state, PlayerId actor, GameCommand command) =>
        GameEngine.Execute(TestRulesets.Default, state, actor, command).ShouldSucceed();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EventsFor_AttackBetweenTwoOthers_IsSeenByAThirdPlayerOnlyInSight(bool thirdWatches)
    {
        var builder = new GameStateBuilder().WithPlayers(P1, P2, P3).WithCapitals();
        builder.AddCity(P3, 0, Height - 1);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Soldier, 4, 3);
        if (thirdWatches)
        {
            builder.AddUnit(P3, TestRulesets.Soldier, 6, 3);
        }

        var state = builder.Build();

        var update = Execute(state, P1, new AttackCommand(attacker, At(4, 3)));

        VisibilityCalculator.EventsFor(TestRulesets.Default, state, update, P3).OfType<UnitAttackedEvent>().Any().ShouldBe(thirdWatches);
    }
}
