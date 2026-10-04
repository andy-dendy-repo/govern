using Govern.Client.Components.Game;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components.Game;

public class TreeViewTests : ClientTestContext
{
    [Fact]
    public void Render_Items_GoOneColumnAfterTheDeepestItemTheyRequire()
    {
        (string Id, string[] Requires)[] items = [("a", []), ("b", []), ("c", ["a"]), ("d", ["c", "b"]), ("e", ["unknown"])];

        var cut = Render<TreeView<(string Id, string[] Requires)>>(parameters => parameters
            .Add(static tree => tree.Items, items)
            .Add(static tree => tree.Key, static item => item.Id)
            .Add(static tree => tree.Requires, static item => item.Requires)
            .Add(static tree => tree.Label, "Sample")
            .Add(static tree => tree.ItemTemplate, static item => $"[{item.Id}]"));

        cut.Find(".tree-view").GetAttribute("aria-label").ShouldBe("Sample");
        cut.FindAll(".tree-column").Select(static column => column.TextContent.Trim()).ShouldBe(["[a][b][e]", "[c]", "[d]"]);
        cut.FindAll("[role=listitem]").Count.ShouldBe(5);
    }

    [Fact]
    public void Render_NoItems_DrawsNoColumns()
    {
        var cut = Render<TreeView<string>>(parameters => parameters
            .Add(static tree => tree.Items, [])
            .Add(static tree => tree.Key, static item => item)
            .Add(static tree => tree.ItemTemplate, static item => item));

        cut.FindAll(".tree-column").ShouldBeEmpty();
    }
}
