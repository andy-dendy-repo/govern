using Govern.Core.State;
using Govern.Domain.Entities;
using Govern.Domain.Enums;

namespace Govern.UnitTests.Domain.Entities;

public class GameTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Creator = Guid.CreateVersion7();
    private static readonly Guid Joiner = Guid.CreateVersion7();

    [Fact]
    public void Create_SeatsCreatorAtZeroInLobbyWithInviteCode()
    {
        var game = Game.Create(Creator, Now);

        game.Phase.ShouldBe(GamePhase.Lobby);
        game.CreatorId.ShouldBe(Creator);
        game.InviteCode.ShouldNotBeNull();
        game.State.ShouldBeNull();
        game.Revision.ShouldBe(0);
        var seat = game.Players.ShouldHaveSingleItem();
        seat.UserId.ShouldBe(Creator);
        seat.Seat.ShouldBe(0);
        seat.GameId.ShouldBe(game.Id);
        seat.JoinedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_EmptyCreator_Throws()
    {
        Should.Throw<ArgumentException>(() => Game.Create(Guid.Empty, Now));
    }

    [Fact]
    public void Create_TwoGames_HaveDifferentInviteCodes()
    {
        Game.Create(Creator, Now).InviteCode.ShouldNotBe(Game.Create(Creator, Now).InviteCode);
    }

    [Fact]
    public void Join_SecondUser_TakesSeatOneAndFillsTheGame()
    {
        var game = Game.Create(Creator, Now);

        var seat = game.Join(Joiner, Now.AddMinutes(1));

        seat.Seat.ShouldBe(1);
        seat.UserId.ShouldBe(Joiner);
        game.IsFull.ShouldBeTrue();
        game.IsPlayer(Joiner).ShouldBeTrue();
        game.FindPlayer(Joiner).ShouldBeSameAs(seat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Join_ExistingPlayer_ReturnsTheirSeatWithoutChanges(bool asCreator)
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        var revision = game.Revision;

        var seat = game.Join(asCreator ? Creator : Joiner, Now.AddMinutes(1));

        seat.Seat.ShouldBe(asCreator ? 0 : 1);
        seat.JoinedAt.ShouldBe(Now);
        game.Players.Count.ShouldBe(2);
        game.Revision.ShouldBe(revision);
    }

    [Fact]
    public void Join_FullGame_Throws()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        Should.Throw<InvalidOperationException>(() => game.Join(Guid.CreateVersion7(), Now));
    }

    [Fact]
    public void Join_StartedGame_ThrowsForNewPlayerButNotForExistingOne()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Join(Guid.CreateVersion7(), Now));
        game.Join(Joiner, Now).Seat.ShouldBe(1);
    }

    [Fact]
    public void Start_CreatorWithFullLobby_CreatesStateWithPlayersInSeatOrder()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        game.Start(Creator);

        game.Phase.ShouldBe(GamePhase.InProgress);
        var state = game.State.ShouldNotBeNull();
        var seats = game.Players.OrderBy(player => player.Seat).Select(player => new PlayerId(player.Id)).ToArray();
        state.Players.Select(player => player.Id).ShouldBe(seats);
        state.CurrentPlayer.ShouldBe(seats[0]);
        state.Status.ShouldBe(GameStatus.InProgress);
    }

    [Fact]
    public void Start_StateContainsNoUserIds()
    {
        var game = StartedGame();

        var ids = game.State!.Players.Select(player => player.Id.Value).Concat(game.State.Units.Select(unit => unit.Owner.Value));

        ids.ShouldNotContain(Creator);
        ids.ShouldNotContain(Joiner);
    }

    [Fact]
    public void Start_NonCreator_Throws()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        Should.Throw<InvalidOperationException>(() => game.Start(Joiner));
        game.Phase.ShouldBe(GamePhase.Lobby);
    }

    [Fact]
    public void Start_OnlyCreatorSeated_Throws()
    {
        var game = Game.Create(Creator, Now);

        Should.Throw<InvalidOperationException>(() => game.Start(Creator));
        game.State.ShouldBeNull();
    }

    [Fact]
    public void Start_Twice_Throws()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Start(Creator));
    }

    [Fact]
    public void Revision_IncrementsOnJoinAndStart()
    {
        var game = Game.Create(Creator, Now);
        game.Revision.ShouldBe(0);

        game.Join(Joiner, Now);
        game.Revision.ShouldBe(1);

        game.Start(Creator);
        game.Revision.ShouldBe(2);
    }

    private static Game StartedGame()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        game.Start(Creator);
        return game;
    }
}
