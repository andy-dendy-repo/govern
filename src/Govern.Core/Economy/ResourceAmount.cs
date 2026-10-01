namespace Govern.Core.Economy;

/// <summary>
/// An amount of one resource: a yield per turn, or what a city holds in its stock.
/// </summary>
/// <param name="Resource">The resource.</param>
/// <param name="Amount">How much of it.</param>
public sealed record ResourceAmount(ResourceType Resource, int Amount);
