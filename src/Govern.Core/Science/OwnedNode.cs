using System.Text.Json.Serialization;

namespace Govern.Core.Science;

/// <summary>
/// A science node a player has unlocked (<see cref="State.Player.Nodes"/>). Nodes are kept in the order they were unlocked, so
/// the newest is disabled first when the player's science no longer covers them all, and the oldest is enabled again first.
/// </summary>
/// <param name="Node">The node's id (<see cref="ScienceNode.Id"/>).</param>
/// <param name="Disabled">
/// Whether the node is switched off because the player's science doesn't cover it: it takes no points, and what it unlocks can't
/// be started until it is enabled again. Left out of the JSON while <see langword="false"/>.
/// </param>
public sealed record OwnedNode(
    string Node,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Disabled = false);
