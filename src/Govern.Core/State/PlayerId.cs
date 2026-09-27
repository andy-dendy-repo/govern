namespace Govern.Core.State;

/// <summary>
/// Identifier of a player. The host application chooses it: Govern uses the per-game player id (<c>GamePlayer.Id</c>), never a
/// user id, so a <see cref="Visibility.PlayerView"/> reveals no account ids. The engine never maps users.
/// </summary>
/// <param name="Value">Non-empty identifier.</param>
public readonly record struct PlayerId(Guid Value);
