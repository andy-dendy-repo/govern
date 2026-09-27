using System.Text.Json;
using Govern.Core.Actions;
using Govern.Core.Cities;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.UnitTests.Core.Builders;
using static Govern.UnitTests.Core.Builders.GameStateBuilder;

namespace Govern.UnitTests.Core.Serialization;

public class GameSerializerTests
{
    [Fact]
    public void DeserializeState_SerializedGame_RoundTripsExactly()
    {
        var state = GameFactory.Create(new GameSetup([P1, P2], Seed: 3));
        var json = GameSerializer.Serialize(state);

        var restored = GameSerializer.DeserializeState(json);

        GameSerializer.Serialize(restored).ShouldBe(json);
        json.ShouldContain("\"status\":\"inProgress\"");
        json.ShouldNotContain("\"definition\"");
    }

    public static TheoryData<GameCommand, string> Commands => new()
    {
        { new MoveUnitCommand(new UnitId(1), At(2, 3)), "move_unit" },
        { new AttackCommand(new UnitId(1), At(2, 3)), "attack" },
        { new FoundCityCommand(new UnitId(1)), "found_city" },
        { new SetProductionCommand(new CityId(4), UnitType.Settler), "set_production" },
        { new EndTurnCommand(), "end_turn" },
        { new ResignCommand(), "resign" },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void Deserialize_Command_RoundTripsWithStableDiscriminator(GameCommand command, string discriminator)
    {
        var json = GameSerializer.Serialize(command);

        json.ShouldStartWith($"{{\"type\":\"{discriminator}\"");
        GameSerializer.Deserialize<GameCommand>(json).ShouldBe(command);
    }

    [Fact]
    public void Deserialize_DiscriminatorAfterOtherProperties_IsAccepted()
    {
        var command = GameSerializer.Deserialize<GameCommand>("""{"unitId":{"value":7},"type":"found_city"}""");

        command.ShouldBe(new FoundCityCommand(new UnitId(7)));
    }

    [Fact]
    public void Deserialize_EnumAsNumber_Throws()
    {
        Should.Throw<JsonException>(() =>
            GameSerializer.Deserialize<GameCommand>("""{"type":"set_production","cityId":{"value":1},"unitType":99}"""));
    }

    [Fact]
    public void Deserialize_Events_RoundTripWithDiscriminators()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, UnitType.Soldier, 3, 3);
        builder.AddUnit(P2, UnitType.Settler, 4, 3);
        var events = GameEngine.Execute(builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed().Events;
        var json = GameSerializer.Serialize(events);

        var restored = GameSerializer.Deserialize<GameEvent[]>(json);

        restored.ShouldBe(events.ToArray());
        json.ShouldContain("\"type\":\"game_won\"");
    }

    [Fact]
    public void DeserializeState_OtherSchemaVersion_Throws()
    {
        var json = GameSerializer.Serialize(new GameStateBuilder().WithCapitals().Build() with { SchemaVersion = 99 });

        Should.Throw<NotSupportedException>(() => GameSerializer.DeserializeState(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"unitId":{"value":1}}""")]
    [InlineData("""{"type":"found_city"}""")]
    [InlineData("""{"type":"teleport"}""")]
    public void Deserialize_MissingDiscriminatorOrRequiredValue_ThrowsJsonException(string json)
    {
        Should.Throw<JsonException>(() => GameSerializer.Deserialize<GameCommand>(json));
    }

    [Fact]
    public void DeserializeState_MissingSchemaVersion_Throws()
    {
        Should.Throw<NotSupportedException>(() => GameSerializer.DeserializeState("""{"version":0}"""));
    }

    [Fact]
    public void DeserializeState_FinishedGameWithCities_RoundTripsExactly()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, UnitType.Soldier, 4, 4);
        var finished = GameEngine.Execute(builder.Build(), P2, new ResignCommand()).ShouldSucceed().State;
        var json = GameSerializer.Serialize(finished);

        var restored = GameSerializer.DeserializeState(json);

        restored.Winner.ShouldBe(P1);
        restored.Cities.Length.ShouldBe(2);
        GameSerializer.Serialize(restored).ShouldBe(json);
    }

    [Fact]
    public void Deserialize_PlayerView_RoundTripsExactly()
    {
        var view = GameEngine.ViewFor(GameFactory.Create(new GameSetup([P1, P2], Seed: 9)), P1);
        var json = GameSerializer.Serialize(view);

        GameSerializer.Serialize(GameSerializer.Deserialize<Govern.Core.Visibility.PlayerView>(json)).ShouldBe(json);
    }

    [Fact]
    public void Deserialize_JsonNull_Throws()
    {
        Should.Throw<JsonException>(() => GameSerializer.Deserialize<GameCommand>("null"));
    }
}
