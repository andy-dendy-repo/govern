using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Map;

// Writes a TerrainType as its plain id ("plains"), the same JSON the former enum produced.
internal sealed class TerrainTypeJsonConverter : JsonConverter<TerrainType>
{
    public override TerrainType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new TerrainType(reader.GetString()!)
            : throw new JsonException("A terrain type must be a string id.");

    public override void Write(Utf8JsonWriter writer, TerrainType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
