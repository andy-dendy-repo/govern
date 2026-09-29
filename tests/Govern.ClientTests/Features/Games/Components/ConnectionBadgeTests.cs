using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Models;
using Govern.ClientTests.Support;

namespace Govern.ClientTests.Features.Games.Components;

public class ConnectionBadgeTests : ClientTestContext
{
    [Theory]
    [InlineData(GameWatchStatus.Connecting, null, "Connecting…")]
    [InlineData(GameWatchStatus.Live, null, "Live")]
    [InlineData(GameWatchStatus.Reconnecting, 4.2d, "Reconnecting in 5 s")]
    [InlineData(GameWatchStatus.Reconnecting, null, "Reconnecting…")]
    [InlineData(GameWatchStatus.Gone, null, "Gone")]
    [InlineData(GameWatchStatus.SignedOut, null, "Signed out")]
    [InlineData(GameWatchStatus.Finished, null, "Finished")]
    public void Render_Status_ShowsItsText(GameWatchStatus status, double? retrySeconds, string text)
    {
        var update = new GameWatchUpdate(status, RetryIn: retrySeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);

        var cut = Render<ConnectionBadge>(parameters => parameters.Add(static badge => badge.Update, update));

        cut.Find(".connection").TextContent.Trim().ShouldBe(text);
    }
}
