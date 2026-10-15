using StackExchange.Redis;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Govern.IntegrationTests.Fixtures.RedisFixture))]

namespace Govern.IntegrationTests.Fixtures;

/// <summary>
/// One Redis container for the tests that run several Api instances (<c>ScaleOut/</c>). Most tests run without Redis, on the
/// in-process implementations; <see cref="FlushAsync"/> empties Redis between scale-out tests.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private ConnectionMultiplexer? _admin;

    /// <summary>The connection string the Api hosts use.</summary>
    public string ConnectionString => _redis.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();
        _admin = await ConnectionMultiplexer.ConnectAsync($"{ConnectionString},allowAdmin=true");
    }

    /// <summary>Deletes every key (counters, leases) left by the previous test.</summary>
    public Task FlushAsync() => Server.FlushDatabaseAsync();

    /// <summary>Drops every pub/sub client connection, as a network blip would; the Api hosts reconnect on their own.</summary>
    public Task KillSubscriptionConnectionsAsync() => Server.ExecuteAsync("CLIENT", "KILL", "TYPE", "pubsub");

    /// <summary>Runs a Redis command as the admin connection (e.g. <c>ACL SETUSER</c>).</summary>
    public Task<RedisResult> ExecuteAsync(string command, params object[] args) => Server.ExecuteAsync(command, args);

    public async ValueTask DisposeAsync()
    {
        if (_admin is not null)
        {
            await _admin.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    private IServer Server => _admin!.GetServers().Single();
}
