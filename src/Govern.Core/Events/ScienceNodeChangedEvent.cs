using Govern.Core.Science;
using Govern.Core.State;

namespace Govern.Core.Events;

/// <summary>
/// A player unlocked a science node, or one of their nodes was disabled or enabled again. Private to that player.
/// </summary>
/// <param name="Player">The player.</param>
/// <param name="Node">The node's id.</param>
/// <param name="Change">What happened to it.</param>
public sealed record ScienceNodeChangedEvent(PlayerId Player, string Node, ScienceNodeChange Change) : GameEvent;
