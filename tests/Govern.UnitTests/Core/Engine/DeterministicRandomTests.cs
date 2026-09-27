using Govern.Core.Engine;

namespace Govern.UnitTests.Core.Engine;

public class DeterministicRandomTests
{
    [Fact]
    public void Next_SameSeed_ProducesSameSequence()
    {
        var first = new DeterministicRandom(123);
        var second = new DeterministicRandom(123);

        var a = Enumerable.Range(0, 20).Select(_ => first.Next(0, 1000)).ToList();
        var b = Enumerable.Range(0, 20).Select(_ => second.Next(0, 1000)).ToList();

        a.ShouldBe(b);
        a.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Next_ManyDraws_StayInRange()
    {
        var random = new DeterministicRandom(7);

        var values = Enumerable.Range(0, 1000).Select(_ => random.Next(-3, 4)).ToList();

        values.ShouldAllBe(value => value >= -3 && value < 4);
        values.Distinct().Count().ShouldBe(7);
    }

    [Fact]
    public void Next_FromSavedState_ContinuesTheSameSequence()
    {
        var original = new DeterministicRandom(99);
        original.Next(0, 10);
        var resumed = new DeterministicRandom(original.State);

        resumed.Next(0, 1_000_000).ShouldBe(original.Next(0, 1_000_000));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    public void Next_EmptyRange_Throws(int min, int max)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DeterministicRandom(1).Next(min, max));
    }
}
