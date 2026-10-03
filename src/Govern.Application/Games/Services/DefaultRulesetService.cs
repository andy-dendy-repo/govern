using Govern.Core.Rulesets;

namespace Govern.Application.Games.Services;

/// <summary>
/// The ruleset every game is played with: <c>rulesets/default/v10.json</c>, embedded in this assembly and parsed once. A
/// singleton; an invalid file stops the first request that needs it, so a broken ruleset never reaches a game.
/// </summary>
/// <remarks>Temporary: stored rulesets, with each game pinned to its own version, replace it.</remarks>
public sealed class DefaultRulesetService
{
    /// <summary>The embedded resource name of the default ruleset.</summary>
    public const string ResourceName = "Govern.Rulesets.default.v10.json";

    private readonly Lazy<Ruleset> _ruleset = new(Load);

    /// <summary>The parsed default ruleset.</summary>
    /// <exception cref="InvalidOperationException">The resource is missing.</exception>
    /// <exception cref="FormatException">The embedded document is not a valid ruleset.</exception>
    public Ruleset Ruleset => _ruleset.Value;

    private static Ruleset Load()
    {
        using var stream = typeof(DefaultRulesetService).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded ruleset '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return Ruleset.ParseValid(reader.ReadToEnd());
    }
}
