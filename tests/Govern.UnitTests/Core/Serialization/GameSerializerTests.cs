using System.Text.Json;
using System.Text.Json.Nodes;
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
        var state = GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 3));
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
        { new SetProductionCommand(new CityId(4), TestRulesets.Settler), "set_production" },
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

    [Theory]
    [InlineData("""{"type":"set_production","cityId":{"value":1},"unitType":null}""")]
    [InlineData("""{"type":"set_production","cityId":{"value":1},"unitType":{"id":"soldier"}}""")]
    public void Deserialize_UnitTypeNotAString_Throws(string json)
    {
        Should.Throw<JsonException>(() => GameSerializer.Deserialize<GameCommand>(json));
    }

    [Fact]
    public void Serialize_UnitType_WritesThePlainRulesetId()
    {
        var command = new SetProductionCommand(new CityId(1), TestRulesets.Soldier);

        var json = GameSerializer.Serialize<GameCommand>(command);

        json.ShouldContain("\"unitType\":\"soldier\"");
        GameSerializer.Deserialize<GameCommand>(json).ShouldBe(command);
    }

    [Fact]
    public void Deserialize_Events_RoundTripWithDiscriminators()
    {
        var builder = new GameStateBuilder();
        builder.AddCity(P1, 0, 0);
        var attacker = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        builder.AddUnit(P2, TestRulesets.Settler, 4, 3);
        var events = GameEngine.Execute(TestRulesets.Default, builder.Build(), P1, new AttackCommand(attacker, At(4, 3))).ShouldSucceed().Events;
        var json = GameSerializer.Serialize(events);

        var restored = GameSerializer.Deserialize<GameEvent[]>(json);

        restored.ShouldBe(events.ToArray());
        json.ShouldContain("\"type\":\"game_won\"");
        json.ShouldContain("\"type\":\"unit_attacked\"");
    }

    [Fact]
    public void DeserializeState_UnitWithoutDamage_ReadsUnhurtAndWritesNoDamageUntilHurt()
    {
        var builder = new GameStateBuilder().WithCapitals();
        var unhurt = builder.AddUnit(P1, TestRulesets.Soldier, 3, 3);
        var hurt = builder.AddUnit(P2, TestRulesets.Soldier, 8, 3, damage: 30);

        var json = GameSerializer.Serialize(builder.Build());
        var restored = GameSerializer.DeserializeState(json);

        // Games saved before health existed have no damage field; they read as unhurt.
        var units = JsonDocument.Parse(json).RootElement.GetProperty("units").EnumerateArray().ToList();
        units.Count(static unit => unit.TryGetProperty("damage", out _)).ShouldBe(1);
        restored.FindUnit(unhurt)!.Damage.ShouldBe(0);
        restored.FindUnit(hurt)!.Damage.ShouldBe(30);
    }

    [Fact]
    public void DeserializeState_TileRivers_RoundTripAndAreLeftOutWhereThereAreNone()
    {
        var builder = new GameStateBuilder().WithCapitals().WithRiver(TestRulesets.DeepRiver, ((3, 3), (4, 3)), ((3, 3), (3, 2)));
        var state = builder.Build();

        var json = GameSerializer.Serialize(state);
        var restored = GameSerializer.DeserializeState(json);

        // Games saved before rivers existed have no rivers field; they read as maps without rivers.
        var tiles = JsonDocument.Parse(json).RootElement.GetProperty("map").GetProperty("tiles").EnumerateArray().ToList();
        tiles.Count(static tile => tile.TryGetProperty("rivers", out _)).ShouldBe(1);
        json.ShouldContain("\"rivers\":{\"east\":\"deep_river\",\"northWest\":\"deep_river\"}");
        restored.Map.Tiles.ShouldBe(state.Map.Tiles);
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
    public void DeserializeState_SchemaVersionOneWithNextId_IsUpgradedAndKeepsIds()
    {
        var state = new GameStateBuilder().WithCapitals().Build();
        var node = JsonNode.Parse(GameSerializer.Serialize(state))!.AsObject();
        node["schemaVersion"] = 1;
        node["nextId"] = 3;

        var upgraded = GameSerializer.DeserializeState(node.ToJsonString());

        upgraded.SchemaVersion.ShouldBe(GameState.CurrentSchemaVersion);
        GameSerializer.Serialize(upgraded).ShouldBe(GameSerializer.Serialize(state));
    }

    [Fact]
    public void DeserializeState_SchemaVersionZero_Throws()
    {
        var json = GameSerializer.Serialize(new GameStateBuilder().WithCapitals().Build() with { SchemaVersion = 0 });

        Should.Throw<NotSupportedException>(() => GameSerializer.DeserializeState(json));
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
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        var finished = GameEngine.Execute(TestRulesets.Default, builder.Build(), P2, new ResignCommand()).ShouldSucceed().State;
        var json = GameSerializer.Serialize(finished);

        var restored = GameSerializer.DeserializeState(json);

        restored.Winner.ShouldBe(P1);
        restored.Cities.Length.ShouldBe(2);
        GameSerializer.Serialize(restored).ShouldBe(json);
    }

    [Fact]
    public void Deserialize_PlayerView_RoundTripsExactly()
    {
        var view = GameEngine.ViewFor(TestRulesets.Default, GameFactory.Create(TestRulesets.Default, new GameSetup([P1, P2], Seed: 9)), P1);
        var json = GameSerializer.Serialize(view);

        GameSerializer.Serialize(GameSerializer.Deserialize<Govern.Core.Visibility.PlayerView>(json)).ShouldBe(json);
    }

    [Fact]
    public void Serialize_PlayerView_WritesNoTileCoordinatesAndNullEnemyProduction()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        builder.AddCity(P2, 6, 4);
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        var json = GameSerializer.Serialize(view);
        var root = JsonDocument.Parse(json).RootElement;

        root.GetProperty("tiles")[0].EnumerateObject().Select(static p => p.Name).ShouldBe(["visibility", "terrain"]);
        var enemy = root.GetProperty("cities").EnumerateArray().Single(city => city.GetProperty("owner").GetProperty("value").GetGuid() == P2.Value);
        enemy.GetProperty("producing").ValueKind.ShouldBe(JsonValueKind.Null);
        enemy.GetProperty("productionProgress").ValueKind.ShouldBe(JsonValueKind.Null);
        GameSerializer.Serialize(GameSerializer.Deserialize<Govern.Core.Visibility.PlayerView>(json)).ShouldBe(json);
    }

    [Fact]
    public void Serialize_PlayerView_WritesNullEnemyMovement()
    {
        var builder = new GameStateBuilder().WithCapitals();
        builder.AddUnit(P1, TestRulesets.Soldier, 4, 4);
        builder.AddUnit(P2, TestRulesets.Soldier, 5, 4);
        var view = GameEngine.ViewFor(TestRulesets.Default, builder.Build(), P1);

        var json = GameSerializer.Serialize(view);
        var units = JsonDocument.Parse(json).RootElement.GetProperty("units").EnumerateArray().ToList();

        units.Select(static unit => string.Join(',', unit.EnumerateObject().Select(static p => p.Name)))
            .ShouldAllBe(names => names == "id,owner,type,position,movementLeft,health");
        units.Single(IsOwnedBy(P2)).GetProperty("movementLeft").ValueKind.ShouldBe(JsonValueKind.Null);
        units.Single(IsOwnedBy(P1)).GetProperty("movementLeft").GetInt32().ShouldBe(2);
        GameSerializer.Serialize(GameSerializer.Deserialize<Govern.Core.Visibility.PlayerView>(json)).ShouldBe(json);
    }

    private static Func<JsonElement, bool> IsOwnedBy(PlayerId player) =>
        unit => unit.GetProperty("owner").GetProperty("value").GetGuid() == player.Value;

    [Fact]
    public void Deserialize_JsonNull_Throws()
    {
        Should.Throw<JsonException>(() => GameSerializer.Deserialize<GameCommand>("null"));
    }
}
