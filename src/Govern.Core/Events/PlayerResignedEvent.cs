using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A player gave up.
/// </summary>
/// <param name="PlayerId">The player.</param>
public sealed record PlayerResignedEvent(PlayerId PlayerId) : GameEvent;
