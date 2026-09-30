using Govern.Client.Components.Game;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class GameIconTests : ClientTestContext
{
    [Fact]
    public void Render_WithoutLabel_IsDecorative()
    {
        var cut = Render<GameIcon>(parameters => parameters.Add(static icon => icon.Id, "unit-settler"));

        cut.Find("use").GetAttribute("href").ShouldBe("#unit-settler");
        cut.Find("svg").GetAttribute("aria-hidden").ShouldBe("true");
        cut.Find("svg").HasAttribute("role").ShouldBeFalse();
    }

    [Fact]
    public void Render_WithLabelAndClass_IsAnImageWithThatName()
    {
        var cut = Render<GameIcon>(parameters => parameters
            .Add(static icon => icon.Id, "city")
            .Add(static icon => icon.Label, "City")
            .Add(static icon => icon.Class, "seat-2"));

        var svg = cut.Find("svg");
        svg.GetAttribute("role").ShouldBe("img");
        svg.GetAttribute("aria-label").ShouldBe("City");
        svg.HasAttribute("aria-hidden").ShouldBeFalse();
        svg.ClassList.ShouldBe(["game-icon", "seat-2"], ignoreOrder: true);
    }
}
