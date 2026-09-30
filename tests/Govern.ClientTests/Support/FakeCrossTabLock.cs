using Govern.Client.Auth;

namespace Govern.ClientTests.Support;

/// <summary>Runs the action directly (a single test has no other tabs), or refuses the lock when <see cref="Unavailable"/>.</summary>
public sealed class FakeCrossTabLock : ICrossTabLock
{
    /// <summary>How many times the lock was taken.</summary>
    public int Taken { get; private set; }

    /// <summary>When set, the lock can't be taken, like a failed module load.</summary>
    public bool Unavailable { get; set; }

    public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new CrossTabLockUnavailableException();
        }

        Taken++;
        return action();
    }
}
