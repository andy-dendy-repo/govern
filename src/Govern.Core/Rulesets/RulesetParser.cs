using System.Text.Json;
using System.Text.Json.Serialization;

namespace Govern.Core.Rulesets;

// Reads a ruleset document strictly: camelCase names only, every field required, no unknown fields, no duplicate keys, and
// numbers and enums in their own JSON types, so a typo fails loading instead of silently taking a default.
internal static class RulesetParser
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static RulesetParseResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        RulesetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<RulesetDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            return new RulesetParseResult(null, [new RulesetProblem(exception.Path ?? "$", WithoutPosition(exception.Message))]);
        }

        if (document is null)
        {
            return new RulesetParseResult(null, [new RulesetProblem("$", "The ruleset must be a JSON object.")]);
        }

        var problems = RulesetValidator.Validate(document);
        return problems.IsEmpty
            ? new RulesetParseResult(new Ruleset(document, Ruleset.ComputeHash(json)), [])
            : new RulesetParseResult(null, problems);
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
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
