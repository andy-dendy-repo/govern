using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Services;

/// <summary>
/// Reads the recent part of a player's game log, which every game response carries: the entries of the previous round and the
/// current one (so always the other players' last turns and the player's own), at most <see cref="MaxEntries"/> of them.
/// </summary>
public static class GameLogService
{
    /// <summary>The most entries a response carries; the oldest are left out beyond it.</summary>
    public const int MaxEntries = 200;

    /// <summary>
    /// Loads the recent log of <paramref name="caller"/>, oldest first; empty before the game has started.
    /// </summary>
    /// <param name="db">The unit of work of the calling handler.</param>
    /// <param name="game">The game (its state decides the current round).</param>
    /// <param name="caller">The caller's seat.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The entries, ordered by sequence.</returns>
    public static async Task<IReadOnlyList<GameLogItem>> RecentAsync(
        IApplicationDbContext db,
        Game game,
        GamePlayer caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(caller);

        if (game.State is not { } state)
        {
            return [];
        }

        var fromTurn = state.Turn - 1;
        var newestFirst = await db.GameLogEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerId == caller.Id && entry.Turn >= fromTurn)
            .OrderByDescending(static entry => entry.Sequence)
            .Take(MaxEntries)
            .Select(GameLogItem.Projection)
            .ToListAsync(cancellationToken);

        newestFirst.Reverse();
        return newestFirst;
    }
}
