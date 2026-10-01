using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

// Writes a ImprovementType as its plain id ("farm").
internal sealed class ImprovementTypeJsonConverter : JsonConverter<ImprovementType>
{
    public override ImprovementType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new ImprovementType(reader.GetString()!)
            : throw new JsonException("A improvement type must be a string id.");

    public override void Write(Utf8JsonWriter writer, ImprovementType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
