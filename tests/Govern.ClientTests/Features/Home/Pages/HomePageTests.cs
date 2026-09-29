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

    [Fact]
    public void Render_Anonymous_LinksToSignInAndRegister()
    {
        var cut = Render<HomePage>();

        Links(cut).ShouldBe(["login", "register"]);
    }

    [Fact]
    public void Render_SignedIn_LinksToGamesAndJoin()
    {
        SignIn();

        var cut = Render<HomePage>();

        Links(cut).ShouldBe(["games", "join"]);
    }

    private static string?[] Links(IRenderedComponent<HomePage> cut) =>
        [.. cut.FindAll(".home-links a").Select(static link => link.GetAttribute("href"))];
}
