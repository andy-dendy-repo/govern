using Govern.Core.Rulesets;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The ruleset the board previews and names units with: <c>rulesets/default/v13.json</c>, embedded in the client and parsed once.
/// It is the same file the Api plays every game with.
/// </summary>
/// <remarks>Temporary: the client will fetch the ruleset each game is pinned to from the Api.</remarks>
public sealed class RulesetService
{
    /// <summary>The embedded resource name of the default ruleset.</summary>
    public const string ResourceName = "Govern.Rulesets.default.v13.json";

    private readonly Lazy<Ruleset> _ruleset = new(Load);

    /// <summary>The parsed default ruleset.</summary>
    /// <exception cref="InvalidOperationException">The resource is missing.</exception>
    /// <exception cref="FormatException">The embedded document is not a valid ruleset.</exception>
    public Ruleset Ruleset => _ruleset.Value;

    private static Ruleset Load()
    {
        using var stream = typeof(RulesetService).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded ruleset '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return Ruleset.ParseValid(reader.ReadToEnd());
    }
}
