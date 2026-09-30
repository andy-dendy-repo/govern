using System.Diagnostics;

namespace Govern.ClientTests.Support;

/// <summary>
/// Waits (in real time, briefly) for background work to reach a state, e.g. a watch loop to send its next request after
/// <c>Time.Advance</c>. Always wait for the state that proves a timer exists before advancing the fake clock.
/// </summary>
public static class Eventually
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Returns once <paramref name="condition"/> holds; fails the test after 5 s.</summary>
    public static async Task HoldsAsync(Func<bool> condition, string because)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed > Timeout)
            {
                throw new ShouldAssertException($"Timed out waiting: {because}");
            }

            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Checks that <paramref name="condition"/> still does not hold after a short real wait.</summary>
    public static async Task NeverAsync(Func<bool> condition, string because)
    {
        await Task.Delay(150, Xunit.TestContext.Current.CancellationToken);
        condition().ShouldBeFalse(because);
    }
}
