using System.Collections.Immutable;
using System.Security.Cryptography;
using Govern.Core.Map;
using Govern.Domain.ValueObjects;

namespace Govern.Application.Games.Services;

/// <summary>
/// The pool of predefined maps a lobby can choose: every <c>maps/&lt;key&gt;.json</c>, embedded in this assembly and parsed
/// once against the default ruleset, in key order. A singleton; an invalid or misnamed file stops the first request that needs
/// it, so a broken map never reaches a game.
/// </summary>
public sealed class MapPoolService(DefaultRulesetService rulesets)
{
    /// <summary>The prefix of every embedded map's resource name; the rest is <c>&lt;key&gt;.json</c>.</summary>
    public const string ResourcePrefix = "Govern.Maps.";

    private readonly Lazy<ImmutableArray<PredefinedMap>> _maps = new(() => Load(rulesets));

    /// <summary>Every map in the pool, in key order; never empty.</summary>
    /// <exception cref="InvalidOperationException">The pool is empty, or a file's name is not its key, or a key is a reserved choice.</exception>
    /// <exception cref="FormatException">An embedded document is not a valid map.</exception>
    public ImmutableArray<PredefinedMap> Maps => _maps.Value;

    /// <summary>Whether a lobby may be created with this map choice.</summary>
    /// <param name="choice"><c>generated</c>, <c>random</c> or a pool map's key.</param>
    /// <returns><see langword="true"/> for the generated or random choice, or the key of a map in the pool.</returns>
    public bool IsChoice(string? choice) =>
        choice == MapChoice.Generated.Value || choice == MapChoice.Random.Value || Maps.Any(map => map.Key == choice);

    /// <summary>The pool map a game with this choice starts on.</summary>
    /// <param name="choice">The lobby's choice.</param>
    /// <returns>
    /// The map the choice names, a random pool map for <see cref="MapChoice.Random"/>, or <see langword="null"/> for a generated
    /// map or a key that is no longer in the pool.
    /// </returns>
    public PredefinedMap? Resolve(MapChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return choice.IsRandom ? Maps[RandomNumberGenerator.GetInt32(Maps.Length)] : Maps.FirstOrDefault(map => map.Key == choice.Key);
    }

    private static ImmutableArray<PredefinedMap> Load(DefaultRulesetService rulesets)
    {
        var assembly = typeof(MapPoolService).Assembly;
        var maps = assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var map = PredefinedMap.ParseValid(rulesets.Ruleset, reader.ReadToEnd());
                if (name != $"{ResourcePrefix}{map.Key}.json" || MapChoice.Create(map.Key).Key is null)
                {
                    throw new InvalidOperationException($"The embedded map '{name}' must be named after its key '{map.Key}', which can't be 'generated' or 'random'.");
                }

                return map;
            })
            .ToImmutableArray();

        return maps.IsEmpty ? throw new InvalidOperationException("The map pool is empty.") : maps;
    }
}
