using Govern.Core.Map;
using Govern.Domain.ValueObjects;
using Govern.UnitTests.Core.Builders;

namespace Govern.UnitTests.Domain.ValueObjects;

public class MapChoiceTests
{
    private static readonly PredefinedMap Basin = new("basin", "The Basin", GameMap.Create(8, 8, TestRulesets.Plains), [HexCoord.FromOffset(1, 1)]);

    [Fact]
    public void Create_ReservedValues_GiveTheGeneratedAndRandomChoices()
    {
        MapChoice.Create("generated").ShouldBe(MapChoice.Generated);
        MapChoice.Create("random").ShouldBe(MapChoice.Random);
        MapChoice.Generated.IsGenerated.ShouldBeTrue();
        MapChoice.Random.IsRandom.ShouldBeTrue();
        MapChoice.Generated.Key.ShouldBeNull();
        MapChoice.Random.Key.ShouldBeNull();
    }

    [Theory]
    [InlineData("generated")]
    [InlineData("random")]
    [InlineData("basin")]
    public void Create_IsIdempotent(string value)
    {
        MapChoice.Create(MapChoice.Create(value).Value).ShouldBe(MapChoice.Create(value));
    }

    [Fact]
    public void Create_AKey_NamesThatMap()
    {
        var choice = MapChoice.Create("basin");

        choice.Key.ShouldBe("basin");
        choice.IsGenerated.ShouldBeFalse();
        choice.IsRandom.ShouldBeFalse();
        choice.ToString().ShouldBe("basin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a_key_that_is_longer_than_32_chars")]
    public void Create_BlankOrTooLong_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => MapChoice.Create(value));
    }

    [Fact]
    public void Fits_AcceptsOnlyTheChosenMap()
    {
        MapChoice.Generated.Fits(null).ShouldBeTrue();
        MapChoice.Generated.Fits(Basin).ShouldBeFalse();
        MapChoice.Random.Fits(Basin).ShouldBeTrue();
        MapChoice.Random.Fits(null).ShouldBeFalse();
        MapChoice.Create("basin").Fits(Basin).ShouldBeTrue();
        MapChoice.Create("other").Fits(Basin).ShouldBeFalse();
        MapChoice.Create("basin").Fits(null).ShouldBeFalse();
    }

    [Fact]
    public void Of_GivesTheStartedChoice()
    {
        MapChoice.Of(null).ShouldBe(MapChoice.Generated);
        MapChoice.Of(Basin).Key.ShouldBe("basin");
    }
}
