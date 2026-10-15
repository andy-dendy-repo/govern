using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// In a simultaneous game (<see cref="TurnMode.Simultaneous"/>), a player ended their turn or took it back. Every player sees it.
/// </summary>
/// <param name="PlayerId">The player.</param>
/// <param name="Ended"><see langword="true"/> when they ended their turn, <see langword="false"/> when they took it back.</param>
public sealed record PlayerEndedTurnEvent(PlayerId PlayerId, bool Ended) : GameEvent;
