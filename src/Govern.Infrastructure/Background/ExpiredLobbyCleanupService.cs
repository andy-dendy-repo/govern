using Govern.Application.Common.Abstractions;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Govern.Infrastructure.Background;

/// <summary>
/// Deletes lobbies that have had no activity for <see cref="Game.LobbyLifetime"/>, every
/// <see cref="LobbyCleanupOptions.Interval"/>, and signals their watchers so open streams end. Games that have started are
/// never touched. The deletes bypass the change tracker, so no domain events are raised; the signal is sent directly.
/// </summary>
internal sealed partial class ExpiredLobbyCleanupService(
    IServiceScopeFactory scopeFactory,
    IGameChangeService gameChanges,
    IOptions<LobbyCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredLobbyCleanupService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    // A background-worker boundary: one failed sweep is logged, and the next tick tries again.
    private async Task SweepSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            var deleted = await SweepAsync(stoppingToken);
            if (deleted > 0)
            {
                Log.LobbiesDeleted(logger, deleted);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Log.SweepFailed(logger, exception);
        }
    }

    private async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IApplicationDbContextFactory>();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = timeProvider.GetUtcNow() - Game.LobbyLifetime;
        var total = 0;
        while (true)
        {
            var ids = await db.Games
                .Where(game => game.Phase == GamePhase.Lobby && game.ActivityAt < cutoff)
                .OrderBy(game => game.ActivityAt)
                .Select(game => game.Id)
                .Take(options.Value.BatchSize)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return total;
            }

            // The predicate is repeated, so a lobby joined or started since the select survives.
            total += await db.Games
                .Where(game => ids.Contains(game.Id) && game.Phase == GamePhase.Lobby && game.ActivityAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
            ids.ForEach(gameChanges.Publish);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2200, Level = LogLevel.Information, Message = "Deleted {Count} expired lobbies")]
        public static partial void LobbiesDeleted(ILogger logger, int count);

        [LoggerMessage(EventId = 2201, Level = LogLevel.Error, Message = "Deleting expired lobbies failed")]
        public static partial void SweepFailed(ILogger logger, Exception exception);
    }
}
