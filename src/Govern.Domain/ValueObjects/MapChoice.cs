using Govern.Core.Map;

namespace Govern.Domain.ValueObjects;

/// <summary>
/// The map a game is played on: a generated one (<see cref="Generated"/>), a pool map by its key
/// (<see cref="PredefinedMap.Key"/>) or, in the lobby only, a random pool map (<see cref="Random"/>), which becomes the key of
/// the map drawn when the game starts.
/// </summary>
public sealed record MapChoice
{
    /// <summary>The longest value: a pool map's key is at most 32 characters.</summary>
    public const int MaxLength = 32;

    private const string GeneratedValue = "generated";
    private const string RandomValue = "random";

    private MapChoice(string value) => Value = value;

    /// <summary>A map generated from the game's seed.</summary>
    public static MapChoice Generated { get; } = new(GeneratedValue);

    /// <summary>Any pool map, drawn when the game starts.</summary>
    public static MapChoice Random { get; } = new(RandomValue);

    /// <summary>The stored text: <c>generated</c>, <c>random</c> or a pool map's key.</summary>
    public string Value { get; }

    /// <summary>Whether the map is generated.</summary>
    public bool IsGenerated => Value == GeneratedValue;

    /// <summary>Whether a pool map is drawn when the game starts.</summary>
    public bool IsRandom => Value == RandomValue;

    /// <summary>The pool map's key, or <see langword="null"/> for a generated or random map.</summary>
    public string? Key => IsGenerated || IsRandom ? null : Value;

    /// <summary>
    /// Creates a choice from input that is already validated (a known pool key, or read from the database).
    /// </summary>
    /// <param name="value"><c>generated</c>, <c>random</c> or a pool map's key.</param>
    /// <returns>The choice.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank or longer than <see cref="MaxLength"/>.</exception>
    public static MapChoice Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength, nameof(value));
        return value switch
        {
            GeneratedValue => Generated,
            RandomValue => Random,
            _ => new MapChoice(value),
        };
    }

    /// <summary>The choice of a started game: the map's key, or <see cref="Generated"/>.</summary>
    /// <param name="map">The pool map played on, or <see langword="null"/> for a generated map.</param>
    /// <returns>The choice.</returns>
    public static MapChoice Of(PredefinedMap? map) => map is null ? Generated : Create(map.Key);

    /// <summary>Whether a game with this choice may start on <paramref name="map"/>.</summary>
    /// <param name="map">The pool map, or <see langword="null"/> for a generated map.</param>
    /// <returns><see langword="true"/> when the map is the one chosen, or any pool map for <see cref="Random"/>.</returns>
    public bool Fits(PredefinedMap? map) => IsGenerated ? map is null : IsRandom ? map is not null : map?.Key == Value;

    /// <summary>The stored text.</summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
