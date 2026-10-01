using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

// Writes a ResourceType as its plain id ("food").
internal sealed class ResourceTypeJsonConverter : JsonConverter<ResourceType>
{
    public override ResourceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new ResourceType(reader.GetString()!)
            : throw new JsonException("A resource type must be a string id.");

    public override void Write(Utf8JsonWriter writer, ResourceType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);
}
