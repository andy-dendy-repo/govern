using Govern.Client.Features.Games.Components;
using Govern.ClientTests.Support;
using Microsoft.JSInterop;

namespace Govern.ClientTests.Features.Games.Components;

public class InviteShareTests : ClientTestContext
{
    private const string Link = $"http://localhost/join#{TestGames.InviteCode}";

    [Fact]
    public void Render_Code_ShowsTheLinkWithTheCodeInItsFragmentAndTheCode()
    {
        var cut = Render<InviteShare>(parameters => parameters.Add(static share => share.Code, TestGames.InviteCode));

        cut.Find(".invite-link").TextContent.ShouldBe(Link);
        cut.Find(".invite-code").TextContent.ShouldBe(TestGames.InviteCode);
    }

    [Fact]
    public void CopyLink_Clicked_CopiesTheLink()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", Link).SetVoidResult();
        var cut = Render<InviteShare>(parameters => parameters.Add(static share => share.Code, TestGames.InviteCode));

        cut.Find("button.copy-link").Click();

        cut.WaitForAssertion(() => cut.Find(".copy-status").TextContent.ShouldBe("Link copied."));
        JSInterop.VerifyInvoke("navigator.clipboard.writeText").Arguments.ShouldBe([Link]);
    }

    [Fact]
    public void CopyCode_Refused_AsksToCopyByHand()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", TestGames.InviteCode).SetException(new JSException("NotAllowedError"));
        var cut = Render<InviteShare>(parameters => parameters.Add(static share => share.Code, TestGames.InviteCode));

        cut.Find("button.copy-code").Click();

        cut.WaitForAssertion(() => cut.Find(".copy-status").TextContent.ShouldContain("copy it"));
    }
}
