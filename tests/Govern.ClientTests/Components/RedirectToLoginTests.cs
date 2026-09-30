using Bunit.TestDoubles;
using Govern.Client.Components;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Components;

public class RedirectToLoginTests : ClientTestContext
{
    [Theory]
    [InlineData("profile", "http://localhost/login?returnUrl=%2Fprofile")]
    [InlineData("games?page=2", "http://localhost/login?returnUrl=%2Fgames%3Fpage%3D2")]
    [InlineData("join#invite-code", "http://localhost/login?returnUrl=%2Fjoin#invite-code")]
    public void Render_Always_ReplacesTheUrlWithLoginKeepingThePathInTheQueryAndTheFragmentAsAFragment(string current, string expected)
    {
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(current);

        Render<RedirectToLogin>();

        navigation.Uri.ShouldBe(expected);
        navigation.History.First().Options.ReplaceHistoryEntry.ShouldBeTrue();
    }
}
