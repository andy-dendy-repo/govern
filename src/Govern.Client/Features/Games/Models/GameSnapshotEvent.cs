namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The game as the caller sees it now. The first event of every connection is one, so a reconnect is a full resync.
/// </summary>
/// <param name="Game">The snapshot.</param>
public sealed record GameSnapshotEvent(GetGameResponse Game) : GameStreamEvent;
