using System.Collections.Immutable;
using Govern.Core.Events;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// One entry of the player's game log: what they saw of one command (already filtered by the server).
/// </summary>
/// <param name="Sequence">1-based position in the player's own log.</param>
/// <param name="Turn">The round the command was sent in.</param>
/// <param name="Events">The events, in order.</param>
public sealed record GameLogItem(int Sequence, int Turn, ImmutableArray<GameEvent> Events);
