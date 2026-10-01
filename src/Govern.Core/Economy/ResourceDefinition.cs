namespace Govern.Core.Economy;

/// <summary>
/// Rules data for one <see cref="ResourceType"/>, from the game's ruleset. Each city keeps resources in its own
/// stock; territory growth spends one of them.
/// </summary>
/// <param name="Id">The resource this definition describes.</param>
public sealed record ResourceDefinition(ResourceType Id);
