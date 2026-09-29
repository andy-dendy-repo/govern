namespace Govern.Client.Features.Games.Models;

/// <summary>
/// An event read from a game's live stream: <see cref="GameSnapshotEvent"/>, <see cref="GameHeartbeatEvent"/>, or the final
/// <see cref="GameStreamClosedEvent"/>.
/// </summary>
public abstract record GameStreamEvent;
