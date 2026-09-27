using Govern.Client.Features.Home.Pages;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Home.Pages;

public class HomePageTests : ClientTestContext
{
    [Fact]
    public void Render_Always_ShowsAppHeading()
    {
        var cut = Render<HomePage>();

        cut.Find("h1").TextContent.ShouldBe("Govern");
    }
}
