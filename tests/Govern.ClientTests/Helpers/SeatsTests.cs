using Govern.Client.Helpers;
using Govern.Core.State;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Helpers;

public class SeatsTests
{
    [Fact]
    public void Class_Players_FollowTheTurnOrder()
    {
        var view = Start();

        Seats.Class(view, You).ShouldBe("seat-1");
        Seats.Class(view, Opponent).ShouldBe("seat-2");
    }

    [Fact]
    public void Number_UnknownPlayer_IsZero()
    {
        Seats.Number(Start(), new PlayerId(Guid.NewGuid())).ShouldBe(0);
    }
}
