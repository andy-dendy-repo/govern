using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

// Writes a BuildingType as its plain id ("campus").
internal sealed class BuildingTypeJsonConverter : JsonConverter<BuildingType>
{
    public override BuildingType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new BuildingType(reader.GetString()!)
            : throw new JsonException("A building type must be a string id.");

    public override void Write(Utf8JsonWriter writer, BuildingType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
