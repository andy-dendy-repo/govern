namespace Govern.Core.State;

/// <summary>
/// SplitMix64 pseudo-random generator. The engine never uses <see cref="System.Random"/>: its <see cref="State"/> is stored in
/// <see cref="GameState.RngState"/>, so replaying the same commands always gives the same game on any .NET version.
/// </summary>
/// <param name="state">Initial state (the seed, or a state saved earlier).</param>
public sealed class DeterministicRandom(ulong state)
{
    /// <summary>Current state; save it to continue the same sequence later.</summary>
    public ulong State { get; private set; } = state;

    /// <summary>Returns the next number in <c>[minInclusive, maxExclusive)</c>.</summary>
    /// <param name="minInclusive">Lower bound, inclusive.</param>
    /// <param name="maxExclusive">Upper bound, exclusive; greater than <paramref name="minInclusive"/>.</param>
    /// <returns>A number in the range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is empty.</exception>
    public int Next(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, minInclusive);

        var range = (ulong)((long)maxExclusive - minInclusive);
        return (int)((long)minInclusive + (long)(NextUInt64() % range));
    }

    private ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15UL;
        var z = State;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
