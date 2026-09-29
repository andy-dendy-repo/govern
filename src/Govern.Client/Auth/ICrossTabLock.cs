namespace Govern.Client.Auth;

/// <summary>
/// A lock shared by all tabs of the app. Refresh tokens are single-use and shared through browser storage, so two tabs
/// refreshing at once would present the same token twice, which the Api treats as reuse and answers by revoking every session.
/// </summary>
public interface ICrossTabLock
{
    /// <summary>Runs <paramref name="action"/> while no other tab holds the lock.</summary>
    /// <param name="action">The work to run under the lock.</param>
    /// <param name="cancellationToken">Cancels waiting for the lock.</param>
    /// <typeparam name="T">The result type.</typeparam>
    /// <returns>The result of <paramref name="action"/>.</returns>
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
}
