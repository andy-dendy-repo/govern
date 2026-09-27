using Govern.Domain.EntityBase;

namespace Govern.Domain.Events;

/// <summary>
/// Raised when a game changes in a way its players may see: a player took a seat, the game started, or a command was applied.
/// Carries only the id; listeners read the game again for each player.
/// </summary>
/// <param name="GameId">The game that changed.</param>
public sealed record GameChangedEvent(Guid GameId) : BaseEvent;
