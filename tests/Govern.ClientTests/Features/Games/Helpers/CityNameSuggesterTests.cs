using Govern.Client.Features.Games.Helpers;

namespace Govern.ClientTests.Features.Games.Helpers;

public class CityNameSuggesterTests
{
    private static readonly string[] Suggestions = ["Ambermoor", "Brightwater", "Cindervale"];

    [Theory]
    [InlineData(0.0, "Ambermoor")]
    [InlineData(0.5, "Brightwater")]
    [InlineData(0.99, "Cindervale")]
    public void Suggest_NothingUsed_PicksByTheRoll(double roll, string expected)
    {
        CityNameSuggester.Suggest(Suggestions, [], current: null, roll).ShouldBe(expected);
    }

    [Fact]
    public void Suggest_SomeUsed_SkipsThemIgnoringCase()
    {
        CityNameSuggester.Suggest(Suggestions, ["ambermoor", "Cindervale"], current: null, 0.0).ShouldBe("Brightwater");
    }

    [Fact]
    public void Suggest_Another_SkipsTheCurrentName()
    {
        CityNameSuggester.Suggest(Suggestions, ["Ambermoor"], current: "Brightwater", 0.0).ShouldBe("Cindervale");
    }

    [Fact]
    public void Suggest_CurrentIsTheOnlyFreeName_KeepsIt()
    {
        CityNameSuggester.Suggest(Suggestions, ["Ambermoor", "Cindervale"], current: "Brightwater", 0.7).ShouldBe("Brightwater");
    }

    [Fact]
    public void Suggest_EveryNameUsed_IsEmpty()
    {
        CityNameSuggester.Suggest(Suggestions, Suggestions, current: null, 0.3).ShouldBeEmpty();
    }
}
