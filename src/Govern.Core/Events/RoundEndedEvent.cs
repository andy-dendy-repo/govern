namespace Govern.Core.Events;

/// <summary>
/// In a simultaneous game (<see cref="State.TurnMode.Simultaneous"/>), every active player had ended their turn, so the round
/// resolved for everyone and the next one began. Every player sees it.
/// </summary>
/// <param name="Turn">Round number of the round that began.</param>
public sealed record RoundEndedEvent(int Turn) : GameEvent;
