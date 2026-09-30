using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A player lost every city and settler.
/// </summary>
/// <param name="PlayerId">The player.</param>
public sealed record PlayerEliminatedEvent(PlayerId PlayerId) : GameEvent;
