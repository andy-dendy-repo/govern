using System.Collections.ObjectModel;
using Govern.Client.Components;
using Govern.Client.Models;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Components;

public class ApiErrorAlertTests : ClientTestContext
{
    [Fact]
    public void Render_NoError_RendersNothing()
    {
        var cut = Render<ApiErrorAlert>();

        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Render_ErrorWithFieldErrors_ShowsTheMessageAndEachFieldMessageAsText()
    {
        var error = new ApiError(400, "validation", "One or more validation errors occurred.",
            new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]> { ["Password"] = ["<b>Too short.</b>"] }));

        var cut = Render<ApiErrorAlert>(parameters => parameters.Add(static alert => alert.Error, error));

        cut.Find(".api-error").TextContent.ShouldContain("One or more validation errors occurred.");
        cut.Find(".api-error").TextContent.ShouldContain("<b>Too short.</b>");
        cut.FindAll(".api-error b").ShouldBeEmpty();
    }
}
