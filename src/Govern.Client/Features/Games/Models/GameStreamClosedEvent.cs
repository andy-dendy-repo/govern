using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The last event of every stream, saying why it ended.
/// </summary>
/// <param name="Reason">Why the stream ended.</param>
public sealed record GameStreamClosedEvent(GameStreamEnd Reason) : GameStreamEvent;
