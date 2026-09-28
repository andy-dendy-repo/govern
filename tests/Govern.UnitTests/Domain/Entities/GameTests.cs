using Govern.Core.Actions;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Domain.Events;

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

        game.Start(Creator, Now);

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

        Should.Throw<InvalidOperationException>(() => game.Start(Joiner, Now));
        game.Phase.ShouldBe(GamePhase.Lobby);
    }

    [Fact]
    public void Start_OnlyCreatorSeated_Throws()
    {
        var game = Game.Create(Creator, Now);

        Should.Throw<InvalidOperationException>(() => game.Start(Creator, Now));
        game.State.ShouldBeNull();
    }

    [Fact]
    public void Start_Twice_Throws()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Start(Creator, Now));
    }

    [Fact]
    public void Revision_IncrementsOnJoinAndStart()
    {
        var game = Game.Create(Creator, Now);
        game.Revision.ShouldBe(0);

        game.Join(Joiner, Now);
        game.Revision.ShouldBe(1);

        game.Start(Creator, Now);
        game.Revision.ShouldBe(2);
    }

    [Fact]
    public void Start_SetsCurrentPlayerToSeatZero()
    {
        var game = StartedGame();

        game.CurrentPlayerId.ShouldBe(game.FindPlayer(Creator)!.Id);
        game.State!.CurrentPlayer.Value.ShouldBe(game.CurrentPlayerId!.Value);
    }

    [Fact]
    public void Execute_AcceptedCommand_ReplacesStateMovesTurnAndIncrementsRevision()
    {
        var game = StartedGame();
        var before = game.State!;

        var result = game.Execute(Creator, new EndTurnCommand(), Now);

        result.IsSuccess.ShouldBeTrue();
        game.State.ShouldBeSameAs(result.Update!.State);
        game.State!.Version.ShouldBe(before.Version + 1);
        game.CurrentPlayerId.ShouldBe(game.FindPlayer(Joiner)!.Id);
        game.Phase.ShouldBe(GamePhase.InProgress);
        game.Revision.ShouldBe(3);
    }

    [Fact]
    public void Execute_RejectedCommand_ChangesNothing()
    {
        var game = StartedGame();
        var before = game.State;

        var result = game.Execute(Joiner, new EndTurnCommand(), Now);

        result.Violation.ShouldBe(RuleViolations.NotYourTurn);
        game.State.ShouldBeSameAs(before);
        game.CurrentPlayerId.ShouldBe(game.FindPlayer(Creator)!.Id);
        game.Revision.ShouldBe(2);
    }

    [Fact]
    public void Execute_Resign_FinishesTheGameWithoutCurrentPlayer()
    {
        var game = StartedGame();

        var result = game.Execute(Joiner, new ResignCommand(), Now);

        result.IsSuccess.ShouldBeTrue();
        game.Phase.ShouldBe(GamePhase.Finished);
        game.CurrentPlayerId.ShouldBeNull();
        game.State!.Status.ShouldBe(GameStatus.Finished);
        game.State.Winner.ShouldBe(game.FindPlayer(Creator)!.PlayerId);
    }

    [Fact]
    public void Execute_InLobby_Throws()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        Should.Throw<InvalidOperationException>(() => game.Execute(Creator, new EndTurnCommand(), Now));
    }

    [Fact]
    public void Execute_Outsider_Throws()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Execute(Guid.CreateVersion7(), new EndTurnCommand(), Now));
    }

    [Fact]
    public void Create_RaisesNoChangeEvent()
    {
        var game = Game.Create(Creator, Now);

        game.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Join_Start_Execute_EachRaiseAChangeEvent()
    {
        var game = Game.Create(Creator, Now);

        game.Join(Joiner, Now);
        game.Start(Creator, Now);
        game.Execute(Creator, new EndTurnCommand(), Now);

        game.DomainEvents.ShouldBe([new GameChangedEvent(game.Id), new GameChangedEvent(game.Id), new GameChangedEvent(game.Id)]);
    }

    [Fact]
    public void Join_Again_Or_RejectedCommand_RaiseNoChangeEvent()
    {
        var game = StartedGame();
        game.ClearDomainEvents();

        game.Join(Joiner, Now);
        game.Execute(Joiner, new EndTurnCommand(), Now);

        game.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Leave_Joiner_FreesTheSeatAndRaisesAChange()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        game.ClearDomainEvents();

        game.Leave(Joiner, Now.AddMinutes(5));

        game.IsPlayer(Joiner).ShouldBeFalse();
        game.Players.ShouldHaveSingleItem().UserId.ShouldBe(Creator);
        game.Revision.ShouldBe(2);
        game.ActivityAt.ShouldBe(Now.AddMinutes(5));
        game.DomainEvents.ShouldBe([new GameChangedEvent(game.Id)]);
    }

    [Fact]
    public void Leave_ThenAnotherUserJoins_TakesSeatOne()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        game.Leave(Joiner, Now);

        var seat = game.Join(Guid.CreateVersion7(), Now);

        seat.Seat.ShouldBe(1);
        game.IsFull.ShouldBeTrue();
    }

    [Fact]
    public void Leave_Creator_Throws()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        Should.Throw<InvalidOperationException>(() => game.Leave(Creator, Now));
        game.Players.Count.ShouldBe(2);
    }

    [Fact]
    public void Leave_NonMember_Throws()
    {
        var game = Game.Create(Creator, Now);

        Should.Throw<InvalidOperationException>(() => game.Leave(Joiner, Now));
    }

    [Fact]
    public void Leave_StartedGame_Throws()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Leave(Joiner, Now));
        game.Players.Count.ShouldBe(2);
    }

    [Fact]
    public void Cancel_CreatorInLobby_RaisesAChange()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        game.ClearDomainEvents();

        game.Cancel(Creator);

        game.DomainEvents.ShouldBe([new GameChangedEvent(game.Id)]);
    }

    [Fact]
    public void Cancel_NonCreator_Throws()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        Should.Throw<InvalidOperationException>(() => game.Cancel(Joiner));
    }

    [Fact]
    public void Cancel_StartedGame_Throws()
    {
        var game = StartedGame();

        Should.Throw<InvalidOperationException>(() => game.Cancel(Creator));
    }

    [Fact]
    public void ActivityAt_SetByCreateJoinAndStart()
    {
        var game = Game.Create(Creator, Now);
        game.ActivityAt.ShouldBe(Now);

        game.Join(Joiner, Now.AddMinutes(1));
        game.ActivityAt.ShouldBe(Now.AddMinutes(1));

        game.Start(Creator, Now.AddMinutes(2));
        game.ActivityAt.ShouldBe(Now.AddMinutes(2));
    }

    [Fact]
    public void ActivityAt_JoinAgain_Unchanged()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);

        game.Join(Joiner, Now.AddMinutes(1));

        game.ActivityAt.ShouldBe(Now);
    }

    [Fact]
    public void ActivityAt_EndTurnAndFinish_Change()
    {
        var game = StartedGame();

        game.Execute(Creator, new EndTurnCommand(), Now.AddMinutes(1));
        game.ActivityAt.ShouldBe(Now.AddMinutes(1));

        game.Execute(Creator, new ResignCommand(), Now.AddMinutes(2));
        game.ActivityAt.ShouldBe(Now.AddMinutes(2));
    }

    [Fact]
    public void ActivityAt_MoveOrRejectedCommand_Unchanged()
    {
        var game = StartedGame();
        var mover = game.FindPlayer(Creator)!.PlayerId;
        var move = game.State!.Units
            .Where(unit => unit.Owner == mover)
            .SelectMany(unit => GameEngine.ReachableTiles(game.State, mover, unit.Id).Select(target => new MoveUnitCommand(unit.Id, target)))
            .First();

        game.Execute(Creator, move, Now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        game.Execute(Joiner, new EndTurnCommand(), Now.AddMinutes(2)).IsSuccess.ShouldBeFalse();

        game.ActivityAt.ShouldBe(Now);
        game.Revision.ShouldBe(3);
    }

    private static Game StartedGame()
    {
        var game = Game.Create(Creator, Now);
        game.Join(Joiner, Now);
        game.Start(Creator, Now);
        return game;
    }
}
