using Govern.Client.Layout;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Layout;

public class MainLayoutTests : ClientTestContext
{
    [Fact]
    public void Render_WithBody_RendersBodyInsideRadzenLayout()
    {
        var cut = Render<MainLayout>(parameters => parameters.Add(static layout => layout.Body, "<p id=\"body\">Page body</p>"));

        cut.Find(".rz-layout main #body").TextContent.ShouldBe("Page body");
    }

    [Fact]
    public void Render_Always_ShowsNavigationToHome()
    {
        var cut = Render<MainLayout>();

        cut.Find(".rz-sidebar .rz-navigation-item").TextContent.ShouldContain("Home");
    }

    [Fact]
    public void SidebarToggle_Clicked_CollapsesSidebar()
    {
        var cut = Render<MainLayout>();
        cut.Find(".rz-sidebar").ClassList.ShouldNotContain("rz-sidebar-collapsed");

        cut.Find(".rz-sidebar-toggle").Click();

        cut.Find(".rz-sidebar").ClassList.ShouldContain("rz-sidebar-collapsed");
    }
}
