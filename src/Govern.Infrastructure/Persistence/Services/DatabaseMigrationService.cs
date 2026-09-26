using Govern.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Govern.Infrastructure.Persistence.Services;

/// <summary>
/// Applies pending EF Core migrations when the host starts, if <see cref="DatabaseOptions.MigrateOnStartup"/> is set.
/// Startup fails when migration fails, so the app never runs against an outdated schema.
/// </summary>
internal sealed partial class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.MigrateOnStartup)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        Log.MigrationsApplied(logger, pending.Count, pending[^1]);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static partial class Log
    {
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Applied {Count} database migration(s); latest is {Migration}")]
        public static partial void MigrationsApplied(ILogger logger, int count, string migration);
    }
}
