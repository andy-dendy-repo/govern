using Govern.Core.Players;

namespace Govern.Core.Events;

/// <summary>
/// The turn passed to the next active player.
/// </summary>
/// <param name="PreviousPlayer">Who ended their turn.</param>
/// <param name="NextPlayer">Who moves now.</param>
/// <param name="Turn">Round number after the change.</param>
public sealed record TurnEndedEvent(PlayerId PreviousPlayer, PlayerId NextPlayer, int Turn) : GameEvent;
