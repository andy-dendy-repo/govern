using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Map;

// Writes a RiverType as its plain id ("stream"), like TerrainType.
internal sealed class RiverTypeJsonConverter : JsonConverter<RiverType>
{
    public override RiverType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new RiverType(reader.GetString()!)
            : throw new JsonException("A river type must be a string id.");

    public override void Write(Utf8JsonWriter writer, RiverType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
