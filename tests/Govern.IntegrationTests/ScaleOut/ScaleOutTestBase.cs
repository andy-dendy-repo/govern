using Govern.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Govern.IntegrationTests.ScaleOut;

/// <summary>
/// Base for tests of several Api instances: two hosts, A and B, each with its own services (game change hub, rate limiters,
/// Redis connection), sharing the test database and the test Redis. The database and Redis are emptied before each test.
/// </summary>
public abstract class ScaleOutTestBase(GovernApiFixture fixture, RedisFixture redis) : IAsyncLifetime
{
    private readonly List<IDisposable> _owned = [];

    protected GovernApiFixture Fixture { get; } = fixture;

    protected RedisFixture Redis { get; } = redis;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        await Fixture.ResetAsync();
        await Redis.FlushAsync();
    }

    /// <summary>Starts an Api instance using <paramref name="redisConnectionString"/> (default: the test Redis).</summary>
    /// <param name="settings">Extra settings, e.g. lower rate limits.</param>
    /// <param name="redisConnectionString">The Redis to use; <see langword="null"/> runs the instance without Redis.</param>
    protected HttpClient StartInstance(IReadOnlyDictionary<string, string>? settings = null, string? redisConnectionString = "")
    {
        var redisSetting = redisConnectionString == "" ? Redis.ConnectionString : redisConnectionString;
        var factory = Fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Redis", redisSetting ?? "");
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }
        });
        var client = factory.CreateClient();
        _owned.Add(client);
        _owned.Add(factory);
        return client;
    }

    public virtual ValueTask DisposeAsync()
    {
        foreach (var owned in Enumerable.Reverse(_owned))
        {
            owned.Dispose();
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
