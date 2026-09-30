using Govern.Client.Components.Game;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.Web;

namespace Govern.ClientTests.Components.Game;

public class ConfirmButtonTests : ClientTestContext
{
    private int _confirmed;

    [Fact]
    public void Click_Once_AsksWithoutActing()
    {
        var cut = RenderButton();

        cut.Find("button.disband").Click();

        cut.Find(".confirm-text").TextContent.ShouldBe("Disband this unit?");
        cut.FindAll("button.disband").ShouldBeEmpty();
        _confirmed.ShouldBe(0);
    }

    [Fact]
    public void Click_Confirm_ActsOnceAndShowsTheButtonAgain()
    {
        var cut = RenderButton();

        cut.Find("button.disband").Click();
        cut.Find("button.confirm-disband").Click();

        _confirmed.ShouldBe(1);
        cut.FindAll("button.disband").Count.ShouldBe(1);
    }

    [Fact]
    public void Click_Dismiss_DoesNothing()
    {
        var cut = RenderButton();

        cut.Find("button.disband").Click();
        cut.Find("button.dismiss").Click();

        _confirmed.ShouldBe(0);
        cut.FindAll("button.disband").Count.ShouldBe(1);
    }

    [Fact]
    public void Escape_WhileAsking_BacksOut()
    {
        var cut = RenderButton();
        cut.Find("button.disband").Click();

        cut.Find(".confirm-button").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        _confirmed.ShouldBe(0);
        cut.FindAll("button.disband").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_Disabled_DisablesTheButton()
    {
        var cut = RenderButton(disabled: true);

        cut.Find("button.disband").HasAttribute("disabled").ShouldBeTrue();
    }

    private IRenderedComponent<ConfirmButton> RenderButton(bool disabled = false) =>
        Render<ConfirmButton>(parameters => parameters
            .Add(static button => button.Text, "Disband")
            .Add(static button => button.Question, "Disband this unit?")
            .Add(static button => button.ConfirmText, "Yes, disband")
            .Add(static button => button.Class, "disband")
            .Add(static button => button.Disabled, disabled)
            .Add(static button => button.Confirmed, () => _confirmed++));
}
