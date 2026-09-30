using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Domain.Enums;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Services;

/// <summary>
/// The per-user limits checked before a user takes a new seat (creating or joining a game): at most
/// <see cref="GameErrors.MaxUnfinishedGamesPerUser"/> unfinished games at a time, and at most
/// <see cref="GameErrors.MaxGamesJoinedPerWindow"/> seats taken within <see cref="GameErrors.QuotaWindow"/>. The counts are
/// not locked, so simultaneous requests can overshoot them: soft caps, bounded by the per-user rate limit.
/// </summary>
public static class GameSeatLimitService
{
    /// <summary>
    /// Returns why <paramref name="userId"/> may not take another seat, or <see langword="null"/> when they may.
    /// </summary>
    /// <param name="db">The unit of work of the calling handler.</param>
    /// <param name="userId">The user about to create or join a game.</param>
    /// <param name="now">The current time of the request.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>
    /// <see cref="GameErrors.TooManyUnfinishedGames"/>, <see cref="GameErrors.DailyQuotaReached"/>, or <see langword="null"/>.
    /// </returns>
    public static async Task<Error?> CheckAsync(IApplicationDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var unfinished = await db.Games.CountAsync(
            game => game.Phase != GamePhase.Finished && game.Players.Any(player => player.UserId == userId), cancellationToken);
        if (unfinished >= GameErrors.MaxUnfinishedGamesPerUser)
        {
            return GameErrors.TooManyUnfinishedGames;
        }

        // Every seat counts, finished games included, so creating and resigning games in a loop cannot grow storage for ever.
        var since = now - GameErrors.QuotaWindow;
        var recent = await db.Games.CountAsync(
            game => game.Players.Any(player => player.UserId == userId && player.JoinedAt >= since), cancellationToken);

        return recent >= GameErrors.MaxGamesJoinedPerWindow ? GameErrors.DailyQuotaReached : null;
    }
}
