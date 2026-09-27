namespace Govern.Core.Cities;

/// <summary>
/// Identifier of a city, unique within one game (allocated from <c>GameState.NextId</c>).
/// </summary>
/// <param name="Value">Positive number.</param>
public readonly record struct CityId(int Value);
