namespace Govern.IntegrationTests.Fixtures;

/// <summary>
/// Base for integration tests: resets the database before each test and exposes a client for the Api.
/// </summary>
public abstract class ApiTestBase(GovernApiFixture fixture) : IAsyncLifetime
{
    protected GovernApiFixture Fixture { get; } = fixture;

    protected HttpClient Client { get; } = fixture.Factory.CreateClient();

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public virtual ValueTask DisposeAsync()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
