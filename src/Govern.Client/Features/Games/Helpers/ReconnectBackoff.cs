namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Delays before reopening a game's live stream. Every delay is at least <see cref="Minimum"/>, which keeps a player under the
/// Api's limit of 30 stream openings per minute.
/// </summary>
public static class ReconnectBackoff
{
    /// <summary>The shortest delay before any reconnect.</summary>
    public static readonly TimeSpan Minimum = TimeSpan.FromSeconds(2);

    /// <summary>The longest exponential step (jitter comes on top).</summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromSeconds(30);

    /// <summary>The shortest delay after the Api refused a stream with 429.</summary>
    public static readonly TimeSpan RateLimitedMinimum = TimeSpan.FromSeconds(15);

    /// <summary>A connection that stayed open this long after its first snapshot counts as healthy: its end resets the failure count.</summary>
    public static readonly TimeSpan HealthyConnection = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CloseSpread = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ExtraSpread = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RateLimitedSpread = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The delay after a healthy connection ended normally (usually at the access token's expiry): 2–5 s, spread so that
    /// tabs don't reconnect at once.
    /// </summary>
    /// <param name="jitter">A random value in [0, 1).</param>
    /// <returns>The delay.</returns>
    public static TimeSpan AfterClose(double jitter) => Minimum + (CloseSpread * Checked(jitter));

    /// <summary>
    /// The delay after <paramref name="failures"/> failed connections in a row. The step is min(30 s, 2^n s); the delay is
    /// spread between half the step and the step plus 1 s, so clients cut off together (e.g. by an Api restart) don't come
    /// back in waves.
    /// </summary>
    /// <param name="failures">Failures in a row, from 1.</param>
    /// <param name="jitter">A random value in [0, 1).</param>
    /// <returns>The delay, at least <see cref="Minimum"/>.</returns>
    public static TimeSpan AfterFailure(int failures, double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failures, 1);

        var step = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(failures, 5)));
        var half = (step < Maximum ? step : Maximum) / 2;
        var delay = half + ((half + ExtraSpread) * Checked(jitter));
        return delay < Minimum ? Minimum : delay;
    }

    /// <summary>
    /// The delay after a 429: like <see cref="AfterFailure"/>, but at least <see cref="RateLimitedMinimum"/> (spread over 5 s).
    /// </summary>
    /// <param name="failures">Failures in a row, from 1.</param>
    /// <param name="jitter">A random value in [0, 1).</param>
    /// <returns>The delay.</returns>
    public static TimeSpan AfterRateLimit(int failures, double jitter)
    {
        var delay = AfterFailure(failures, jitter);
        var floor = RateLimitedMinimum + (RateLimitedSpread * jitter);
        return delay < floor ? floor : delay;
    }

    private static double Checked(double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(jitter, 1d);
        return jitter;
    }
}
