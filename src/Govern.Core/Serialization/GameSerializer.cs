using System.Text.Json;
using System.Text.Json.Serialization;
using Govern.Core.State;

namespace Govern.Core.Serialization;

/// <summary>
/// The one JSON format for game data, shared by storage (jsonb), the Api and the client: camelCase names, enums as camelCase
/// strings, <c>type</c> discriminators for commands and events (accepted in any position), and every constructor value required.
/// </summary>
public static class GameSerializer
{
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

    /// <summary>Deserializes a stored game after checking its schema version, before any binding happens.</summary>
    /// <param name="json">The JSON text of a <see cref="GameState"/>.</param>
    /// <returns>The state.</returns>
    /// <exception cref="JsonException">The JSON is invalid or not an object.</exception>
    /// <exception cref="NotSupportedException">
    /// The snapshot has no <c>schemaVersion</c> or one other than <see cref="GameState.CurrentSchemaVersion"/> (migrate it first).
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
        if (!hasVersion || version.GetInt32() != GameState.CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Only game schema version {GameState.CurrentSchemaVersion} is supported.");
        }

        return document.RootElement.Deserialize<GameState>(Options) ?? throw new JsonException("The JSON document is null.");
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
