namespace Govern.Client.Features.Games.Models;

/// <summary>
/// Sent by the Api after a silent interval (20 s), so a dead connection can be told from a quiet game.
/// </summary>
public sealed record GameHeartbeatEvent : GameStreamEvent;
