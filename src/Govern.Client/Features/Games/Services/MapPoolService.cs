using System.Collections.Immutable;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The map pool, for the lobby's map picker and the map preview: every <c>maps/&lt;key&gt;.json</c>, embedded in the client
/// and parsed once against the client's ruleset, in key order. They are the same files the Api offers, so a key the client
/// shows is one the Api accepts.
/// </summary>
public sealed class MapPoolService(RulesetService rulesets)
{
    /// <summary>The choice that draws a random pool map when the game starts; the picker's default.</summary>
    public const string Random = "random";

    /// <summary>The choice of a generated map.</summary>
    public const string Generated = "generated";

    /// <summary>The prefix of every embedded map's resource name; the rest is <c>&lt;key&gt;.json</c>.</summary>
    public const string ResourcePrefix = "Govern.Maps.";

    private readonly Lazy<ImmutableArray<PredefinedMap>> _maps = new(() => Load(rulesets));

    private readonly Dictionary<int, IReadOnlyList<MapOption>> _options = [];

    /// <summary>Every pool map, in key order.</summary>
    /// <exception cref="FormatException">An embedded document is not a valid map.</exception>
    /// <exception cref="InvalidOperationException">A file's name is not its key, or a key is a reserved choice.</exception>
    public ImmutableArray<PredefinedMap> Maps => _maps.Value;

    /// <summary>
    /// The picker's choices for a game of this many seats: a random pool map first (the default), then each pool map made for
    /// that many players, then a generated map. Without a pool map for the seats, random gives a generated map too.
    /// </summary>
    /// <param name="seats">The game's seats.</param>
    /// <returns>The choices; built once per seat count, so the picker's data keeps its reference between renders.</returns>
    public IReadOnlyList<MapOption> OptionsFor(int seats)
    {
        if (!_options.TryGetValue(seats, out var options))
        {
            options =
            [
                new(Random, NameOf(Random)),
                .. Maps.Where(map => map.Players == seats).Select(static map => new MapOption(map.Key, map.Name)),
                new(Generated, NameOf(Generated)),
            ];
            _options[seats] = options;
        }

        return options;
    }

    /// <summary>The name players see for a game's map (<see cref="GetGameResponse.Map"/>).</summary>
    /// <param name="map"><c>random</c>, <c>generated</c> or a pool map's key.</param>
    /// <returns>The map's name; a key the client doesn't know is shown as it is.</returns>
    public string NameOf(string? map) => map switch
    {
        null or Generated => "Generated map",
        Random => "Random map from the pool",
        _ => Maps.FirstOrDefault(pool => pool.Key == map)?.Name ?? map,
    };

    private static ImmutableArray<PredefinedMap> Load(RulesetService rulesets)
    {
        var assembly = typeof(MapPoolService).Assembly;
        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(name =>
                {
                    using var stream = assembly.GetManifestResourceStream(name)!;
                    using var reader = new StreamReader(stream);
                    var map = PredefinedMap.ParseValid(rulesets.Ruleset, reader.ReadToEnd());
                    return name == $"{ResourcePrefix}{map.Key}.json" && map.Key is not (Random or Generated)
                        ? map
                        : throw new InvalidOperationException($"The embedded map '{name}' must be named after its key '{map.Key}', which can't be '{Random}' or '{Generated}'.");
                }),
        ];
    }
}
