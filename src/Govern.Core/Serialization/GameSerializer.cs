using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.State;

namespace Govern.Core.Serialization;

/// <summary>
/// The one JSON format for game data, shared by storage (jsonb), the Api and the client: camelCase names, enums as camelCase
/// strings, <c>type</c> discriminators for commands and events (accepted in any position), and every constructor value required.
/// </summary>
public static class GameSerializer
{
    // The last schema with the shared nextId counter; the oldest one still read.
    private const int NextIdSchemaVersion = 1;

    // The first schema with city territory and city stocks.
    private const int TerritorySchemaVersion = 3;

    // A frozen historical number for the upgrade only, not a rule: the work area of every version 2 ruleset (v4 and v5
    // `cities.workRadius`) was the tiles next to a city.
    private const int LegacyWorkRadius = 1;

    /// <summary>Read-only serializer options; pass them to any serializer that reads or writes game types.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Serializes a game value (state, command, event, view).</summary>
    /// <typeparam name="T">The declared type; use the base type (e.g. <c>GameCommand</c>) to write the discriminator.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserializes a game value, e.g. a command sent by a client.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="JsonException">
    /// The JSON is invalid, <c>null</c>, lacks a required value or a <c>type</c> discriminator, or does not match <typeparamref name="T"/>.
    /// </exception>
    public static T Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("The JSON document is null.");
        }
        catch (NotSupportedException exception)
        {
            // A polymorphic value without a discriminator: bad input, not a server fault.
            throw new JsonException(exception.Message, exception);
        }
    }

    /// <summary>
    /// Deserializes a stored game after checking its schema version, before any binding happens. Older versions are upgraded:
    /// version 1 carried a <c>nextId</c> counter, which is dropped (its ids stay; new ids are drawn from the generator). Up to
    /// version 2 resources were a per-player stockpile and cities had a work area of the tiles next to them: each player's
    /// stockpile goes to their first city (by id), and each city (by id) claims its tile and the unowned tiles next to it.
    /// </summary>
    /// <param name="json">The JSON text of a <see cref="GameState"/>.</param>
    /// <returns>The state, at <see cref="GameState.CurrentSchemaVersion"/>.</returns>
    /// <exception cref="JsonException">The JSON is invalid or not an object.</exception>
    /// <exception cref="NotSupportedException">
    /// The snapshot has no <c>schemaVersion</c>, or one that is neither current nor a known older version.
    /// </exception>
    public static GameState DeserializeState(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A game state must be a JSON object.");
        }

        var hasVersion = document.RootElement.TryGetProperty("schemaVersion", out var version)
            && version.ValueKind == JsonValueKind.Number;
        if (!hasVersion || !version.TryGetInt32(out var schemaVersion) || schemaVersion is < NextIdSchemaVersion or > GameState.CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Only game schema versions {NextIdSchemaVersion} to {GameState.CurrentSchemaVersion} are supported.");
        }

        // Version 1 differs only by the extra nextId member, which binding skips.
        if (schemaVersion >= TerritorySchemaVersion)
        {
            return document.RootElement.Deserialize<GameState>(Options) ?? throw new JsonException("The JSON document is null.");
        }

        var root = JsonNode.Parse(json)!.AsObject();
        var stockpiles = TakeStockpiles(root);
        var state = root.Deserialize<GameState>(Options) ?? throw new JsonException("The JSON document is null.");
        return UpgradeToTerritory(state, stockpiles) with { SchemaVersion = GameState.CurrentSchemaVersion };
    }

    // Removes the version 2 players' "resources" (Player no longer has them) and returns them by player.
    private static Dictionary<PlayerId, ImmutableArray<ResourceAmount>> TakeStockpiles(JsonObject root)
    {
        var stockpiles = new Dictionary<PlayerId, ImmutableArray<ResourceAmount>>();
        if (root["players"] is not JsonArray players)
        {
            return stockpiles;
        }

        foreach (var player in players.OfType<JsonObject>())
        {
            if (player["resources"] is { } resources && player["id"] is { } id)
            {
                stockpiles[id.Deserialize<PlayerId>(Options)] = resources.Deserialize<ImmutableArray<ResourceAmount>>(Options);
                player.Remove("resources");
            }
        }

        return stockpiles;
    }

    // Version 2 cities owned no tiles: each claims its tile and its unowned neighbours (the old work area of radius 1), and the
    // players' stockpiles move into their first city. The amounts keep the order the old stockpile had (the ruleset's).
    private static GameState UpgradeToTerritory(GameState state, Dictionary<PlayerId, ImmutableArray<ResourceAmount>> stockpiles)
    {
        var map = state.Map;
        foreach (var city in state.Cities.OrderBy(static city => city.Id.Value))
        {
            foreach (var coord in city.Position.WithinRadius(LegacyWorkRadius).Where(map.Contains))
            {
                if (map.TileAt(coord) is { Territory: null } tile)
                {
                    map = map.WithTile(tile with { Territory = city.Id });
                }
            }
        }

        var cities = state.Cities;
        foreach (var (player, stockpile) in stockpiles.Where(static entry => !entry.Value.IsDefaultOrEmpty))
        {
            if (cities.Where(city => city.Owner == player).OrderBy(static city => city.Id.Value).FirstOrDefault() is { } first)
            {
                cities = cities.Replace(first, first with { Stock = stockpile });
            }
        }

        return state with { Map = map, Cities = cities };
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
