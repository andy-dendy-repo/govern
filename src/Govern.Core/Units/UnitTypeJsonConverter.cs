using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Units;

// Writes a UnitType as its plain id ("settler"), the same JSON the former enum produced.
internal sealed class UnitTypeJsonConverter : JsonConverter<UnitType>
{
    public override UnitType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new UnitType(reader.GetString()!)
            : throw new JsonException("A unit type must be a string id.");

    public override void Write(Utf8JsonWriter writer, UnitType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
