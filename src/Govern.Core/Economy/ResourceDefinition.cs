namespace Govern.Core.Economy;

/// <summary>
/// Rules data for one <see cref="ResourceType"/>, from the game's ruleset. Players collect resources in a stockpile; nothing
/// spends them yet.
/// </summary>
/// <param name="Id">The resource this definition describes.</param>
public sealed record ResourceDefinition(ResourceType Id);
