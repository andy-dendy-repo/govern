using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Client.Models;

/// <summary>
/// JSON options for Api calls: the same web defaults (camelCase) and camelCase string enums as the Api, which rejects
/// numeric enum values.
/// </summary>
public static class ApiJson
{
    /// <summary>The shared, read-only serializer options.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
