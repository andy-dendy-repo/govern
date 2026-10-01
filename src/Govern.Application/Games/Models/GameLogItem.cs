using System.Collections.Immutable;
using System.Linq.Expressions;
using Govern.Core.Events;
using Govern.Domain.Entities;

namespace Govern.Application.Games.Models;

/// <summary>
/// One entry of a player's game log: what they saw of one accepted command.
/// </summary>
/// <param name="Sequence">1-based position in the player's own log; a client compares it to find entries it hasn't shown yet.</param>
/// <param name="Turn">The round the command was sent in.</param>
/// <param name="Events">The events the player may see, in order (already filtered by visibility).</param>
public sealed record GameLogItem(int Sequence, int Turn, ImmutableArray<GameEvent> Events)
{
    /// <summary>Projects a stored entry.</summary>
    public static Expression<Func<GameLogEntry, GameLogItem>> Projection { get; } =
        entry => new GameLogItem(entry.Sequence, entry.Turn, entry.Events);
}
