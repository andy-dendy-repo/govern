namespace Govern.Core.Players;

/// <summary>
/// Identifier of a player. The host application chooses it (Govern uses the user id), so the engine never maps users.
/// </summary>
/// <param name="Value">Non-empty identifier.</param>
public readonly record struct PlayerId(Guid Value);
