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

        // MigrateAsync checks for the history table itself, so a fresh database doesn't log a failed query.
        await dbContext.Database.MigrateAsync(cancellationToken);
        var latestMigration = dbContext.Database.GetMigrations().LastOrDefault() ?? "none";
        Log.SchemaUpToDate(logger, latestMigration);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static partial class Log
    {
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Database schema is up to date at migration {Migration}")]
        public static partial void SchemaUpToDate(ILogger logger, string migration);
    }
}
