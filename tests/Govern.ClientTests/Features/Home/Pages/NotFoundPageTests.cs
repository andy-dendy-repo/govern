using Govern.Client.Features.Home.Pages;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Home.Pages;

public class NotFoundPageTests : ClientTestContext
{
    [Fact]
    public void Render_Always_ShowsNotFoundHeadingAndLinkHome()
    {
        var cut = Render<NotFoundPage>();

        cut.Find("h1").TextContent.ShouldBe("Not found");
        cut.Find("a").TextContent.ShouldContain("Back to home");
    }
}
