using Govern.Core.Players;

namespace Govern.Core.Events;

/// <summary>
/// Only one active player is left; the game is finished.
/// </summary>
/// <param name="Winner">The winner.</param>
public sealed record GameWonEvent(PlayerId Winner) : GameEvent;
