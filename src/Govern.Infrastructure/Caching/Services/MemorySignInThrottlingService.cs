using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Options;
using Govern.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Govern.Infrastructure.Caching.Services;

/// <summary>
/// In-process <see cref="ISignInThrottlingService"/>: a size-limited cache of attempt windows keyed by normalised email.
/// Counters are per instance and reset on restart, which is enough for a single Web App instance (with Redis configured,
/// <see cref="RedisSignInThrottlingService"/> shares them and falls back to this one while Redis is unreachable). The window
/// is judged with <see cref="TimeProvider"/>; cache expiry only frees memory.
/// </summary>
internal sealed class MemorySignInThrottlingService : ISignInThrottlingService, IDisposable
{
    private readonly MemoryCache _attempts;
    private readonly Lock _gate = new();
    private readonly SignInThrottlingOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service with its own cache, bounded by <see cref="SignInThrottlingOptions.MaxTrackedAccounts"/>.</summary>
    /// <param name="options">Throttling limits.</param>
    /// <param name="timeProvider">Clock used to judge windows.</param>
    public MemorySignInThrottlingService(IOptions<SignInThrottlingOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options.Value;
        _timeProvider = timeProvider;
        _attempts = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.MaxTrackedAccounts });
    }

    /// <inheritdoc />
    public ValueTask<bool> TryBeginAttemptAsync(Email email, CancellationToken cancellationToken) =>
        ValueTask.FromResult(TryBeginAttempt(email));

    /// <inheritdoc />
    public ValueTask ResetAsync(Email email, CancellationToken cancellationToken)
    {
        Reset(email);
        return ValueTask.CompletedTask;
    }

    /// <summary>Reserves an attempt synchronously; see <see cref="TryBeginAttemptAsync"/>.</summary>
    /// <param name="email">The normalised email being signed in.</param>
    /// <returns><see langword="false"/> when the limit is reached.</returns>
    public bool TryBeginAttempt(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Check and increment under one lock: parallel attempts can't all pass before the first one is counted.
        lock (_gate)
        {
            var current = CurrentWindow(email);
            if (current?.Count >= _options.MaxFailedAttempts)
            {
                return false;
            }

            var next = current is { } window
                ? window with { Count = window.Count + 1 }
                : (Count: 1, StartedAt: _timeProvider.GetUtcNow());

            // NeverRemove: size compaction must not evict live counters (that would unblock an attacked account); when
            // the cache is full a new entry is simply not stored (fail open). Expiry is renewed on every write, so it
            // always outlives the window judged by TimeProvider.
            _attempts.Set(email.Value, next, new MemoryCacheEntryOptions
            {
                Size = 1,
                Priority = CacheItemPriority.NeverRemove,
                AbsoluteExpirationRelativeToNow = _options.Window,
            });
            return true;
        }
    }

    /// <summary>Forgets the attempts of <paramref name="email"/>; see <see cref="ResetAsync"/>.</summary>
    /// <param name="email">The normalised email that signed in.</param>
    public void Reset(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        lock (_gate)
        {
            _attempts.Remove(email.Value);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _attempts.Dispose();

    private (int Count, DateTimeOffset StartedAt)? CurrentWindow(Email email) =>
        _attempts.TryGetValue(email.Value, out (int Count, DateTimeOffset StartedAt) window)
        && _timeProvider.GetUtcNow() - window.StartedAt < _options.Window
            ? window
            : null;
}
