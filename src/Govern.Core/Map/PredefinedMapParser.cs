using System.Text.Json;
using System.Text.Json.Serialization;
using Govern.Core.Rulesets;

namespace Govern.Core.Map;

// Reads a map document as strictly as a ruleset (RulesetParser): camelCase names only, every field required, no unknown fields,
// no duplicate keys and enums as camelCase strings, so a typo fails loading instead of silently taking a default.
internal static class PredefinedMapParser
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static PredefinedMapParseResult Parse(Ruleset ruleset, string json)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(json);

        PredefinedMapDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PredefinedMapDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            return new PredefinedMapParseResult(null, [new RulesetProblem(exception.Path ?? "$", WithoutPosition(exception.Message))]);
        }

        if (document is null)
        {
            return new PredefinedMapParseResult(null, [new RulesetProblem("$", "The map must be a JSON object.")]);
        }

        var problems = new List<RulesetProblem>();
        var map = PredefinedMapValidator.Validate(ruleset, document, problems);
        return problems.Count == 0 && map is not null ? new PredefinedMapParseResult(map, []) : new PredefinedMapParseResult(null, [.. problems]);
    }

    // System.Text.Json appends " Path: … | LineNumber: … | BytePositionInLine: …"; the path is reported separately.
    private static string WithoutPosition(string message)
    {
        var index = message.IndexOf(" Path: ", StringComparison.Ordinal);
        return index < 0 ? message : message[..index];
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
