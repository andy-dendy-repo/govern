using Govern.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Govern.IntegrationTests.Fixtures.GovernApiFixture))]
// Tests share one database that is reset per test, so they must not run concurrently.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Govern.IntegrationTests.Fixtures;

/// <summary>
/// One Postgres container and one Api host for the whole test assembly. Migrations are applied by the Api at
/// startup; <see cref="ResetAsync"/> empties all tables (Respawn) between tests.
/// </summary>
public sealed class GovernApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner? _respawner;

    public GovernApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new GovernApiFactory(ConnectionString);
        _ = Factory.Server; // starts the host, which applies migrations

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    /// <summary>Empties all tables and captured emails.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        Factory.Emails.Clear();
    }

    /// <summary>Opens a database context from the Api's own DI container to arrange or assert persisted state.</summary>
    public async Task<(AsyncServiceScope Scope, IApplicationDbContext Db)> OpenDbAsync()
    {
        var scope = Factory.Services.CreateAsyncScope();
        var db = await scope.ServiceProvider.GetRequiredService<IApplicationDbContextFactory>().CreateDbContextAsync(CancellationToken.None);
        return (scope, db);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
