using Govern.Client.Features.Games.Helpers;

namespace Govern.ClientTests.Features.Games.Helpers;

public class ReconnectBackoffTests
{
    [Theory]
    [InlineData(0d, 2d)]
    [InlineData(0.5d, 3.5d)]
    [InlineData(0.999d, 4.997d)]
    public void AfterClose_Jitter_SpreadsBetweenTwoAndFiveSeconds(double jitter, double seconds)
    {
        ReconnectBackoff.AfterClose(jitter).TotalSeconds.ShouldBe(seconds, 0.001);
    }

    [Theory]
    [InlineData(1, 2d, 3d)]
    [InlineData(2, 2d, 5d)]
    [InlineData(3, 4d, 9d)]
    [InlineData(4, 8d, 17d)]
    [InlineData(5, 15d, 31d)]
    [InlineData(50, 15d, 31d)]
    public void AfterFailure_Jitter_SpreadsFromHalfTheStepToTheStepPlusOneSecond(int failures, double lowest, double highest)
    {
        ReconnectBackoff.AfterFailure(failures, 0).TotalSeconds.ShouldBe(lowest, 0.001);
        ReconnectBackoff.AfterFailure(failures, 0.9999).TotalSeconds.ShouldBe(highest, 0.01);
    }

    [Theory]
    [InlineData(1, 0d, 15d)]
    [InlineData(1, 0.5d, 17.5d)]
    [InlineData(5, 0d, 15d)]
    [InlineData(9, 0.9999d, 31d)]
    public void AfterRateLimit_WaitsAtLeastFifteenSeconds(int failures, double jitter, double seconds)
    {
        ReconnectBackoff.AfterRateLimit(failures, jitter).TotalSeconds.ShouldBe(seconds, 0.01);
    }

    [Theory]
    [InlineData(-0.1d)]
    [InlineData(1d)]
    public void AfterClose_JitterOutOfRange_Throws(double jitter)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ReconnectBackoff.AfterClose(jitter));
    }

    [Fact]
    public void AfterFailure_NoFailures_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ReconnectBackoff.AfterFailure(0, 0));
    }
}
