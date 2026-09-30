namespace Govern.Core.Cities;

/// <summary>
/// Identifier of a city, unique within one game and drawn at random from the game's generator, so it reveals nothing about how many were created.
/// </summary>
/// <param name="Value">Positive number.</param>
public readonly record struct CityId(int Value);
