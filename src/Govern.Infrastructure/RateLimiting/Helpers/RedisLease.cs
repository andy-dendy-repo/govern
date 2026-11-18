using System.Threading.RateLimiting;

namespace Govern.Infrastructure.RateLimiting.Helpers;

/// <summary>A lease from a Redis-backed limiter, with an optional release action run once on dispose.</summary>
/// <param name="isAcquired">Whether the permit was granted.</param>
/// <param name="release">Gives the permit back (concurrency leases); <see langword="null"/> when there is nothing to release.</param>
internal sealed class RedisLease(bool isAcquired, Action? release = null) : RateLimitLease
{
    private Action? _release = release;

    /// <summary>A granted lease with nothing to release.</summary>
    public static RedisLease Acquired { get; } = new(true);

    /// <summary>A rejected lease.</summary>
    public static RedisLease Rejected { get; } = new(false);

    /// <inheritdoc />
    public override bool IsAcquired { get; } = isAcquired;

    /// <inheritdoc />
    public override IEnumerable<string> MetadataNames => [];

    /// <inheritdoc />
    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        metadata = null;
        return false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }

        base.Dispose(disposing);
    }
}
