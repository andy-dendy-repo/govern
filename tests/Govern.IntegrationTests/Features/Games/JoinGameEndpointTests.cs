using System.Net;
using System.Net.Http.Json;
using Govern.Application.Games.Contracts;
using Govern.Application.Games.Models;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Games;

public class JoinGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_ValidCode_Returns200AndSeatsJoinerAtSeatOne()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JoinGameResponse>(Ct))!.GameId.ShouldBe(game.Id);
        (await SeatsAsync(game.Id)).ShouldBe([(0, creator.UserId), (1, joiner.UserId)]);
    }

    [Fact]
    public async Task Post_AlreadyAPlayer_Returns200WithoutChanges()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var again = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);
        var creatorJoins = await GamesApi.JoinAsync(Client, creator.AccessToken, game.InviteCode, Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        creatorJoins.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SeatsAsync(game.Id)).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAA+/")]
    public async Task Post_MalformedCode_Returns400(string inviteCode)
    {
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, inviteCode, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "InviteCode");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var response = await GamesApi.JoinAsync(Client, accessToken: null, "AAAAAAAAAAAAAAAAAAAAAA", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_UnknownCode_Returns404()
    {
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, "AAAAAAAAAAAAAAAAAAAAAA", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.invite_not_found", Ct);
    }

    [Fact]
    public async Task Post_CodeInDifferentCase_Returns404()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);
        var swapped = new string([.. game.InviteCode.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c))]);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, swapped, Ct);

        swapped.ShouldNotBe(game.InviteCode);
        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.invite_not_found", Ct);
    }

    [Fact]
    public async Task Post_FullLobby_Returns409()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        var third = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, third.AccessToken, game.InviteCode, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.full", Ct);
    }

    [Fact]
    public async Task Post_StartedGame_Returns409()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);
        var third = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, third.AccessToken, game.InviteCode, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
    }

    [Fact]
    public async Task Post_ExistingPlayerAfterStart_Returns200()
    {
        var (game, _, joiner) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_SeveralUsersConcurrently_SeatsExactlyOneAndRejectsTheOthersAsFull()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiners = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => GamesApi.NewPlayerAsync(Client, Ct)));
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        var responses = await GameRowLock.RaceAsync(Fixture.ConnectionString, game.Id, joiners.Length, () => Task.WhenAll(
            joiners.Select(j => GamesApi.JoinAsync(Client, j.AccessToken, game.InviteCode, Ct))), Ct);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            await rejected.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.full", Ct);
        }

        var seats = await SeatsAsync(game.Id);
        seats.Count.ShouldBe(2);
        seats[1].UserId.ShouldBeOneOf([.. joiners.Select(j => j.UserId)]);
    }

    [Fact]
    public async Task Post_SameUserConcurrently_ReturnsAll200AndSeatsOnce()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        var responses = await GameRowLock.RaceAsync(Fixture.ConnectionString, game.Id, 5, () => Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct))), Ct);

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        (await SeatsAsync(game.Id)).ShouldBe([(0, creator.UserId), (1, joiner.UserId)]);
    }

    [Fact]
    public async Task Post_NewPlayerAtUnfinishedGameLimit_Returns409()
    {
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        var caller = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, other.UserId, caller.UserId, GameErrors.MaxUnfinishedGamesPerUser, Ct);
        var host = await GamesApi.NewPlayerAsync(Client, Ct);
        var lobby = await GamesApi.CreateGameAsync(Client, host, Ct);

        var response = await GamesApi.JoinAsync(Client, caller.AccessToken, lobby.InviteCode, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.too_many_unfinished_games", Ct);
    }

    [Fact]
    public async Task Post_SeatedPlayerAtUnfinishedGameLimit_Returns200()
    {
        var (game, _, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        var other = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedJoinedLobbiesAsync(Fixture, other.UserId, joiner.UserId, GameErrors.MaxUnfinishedGamesPerUser, Ct);

        var response = await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<List<(int Seat, Guid UserId)>> SeatsAsync(Guid gameId)
    {
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var seats = await db.GamePlayers.AsNoTracking()
                .Where(p => p.GameId == gameId)
                .OrderBy(p => p.Seat)
                .Select(p => new { p.Seat, p.UserId })
                .ToListAsync(Ct);
            return [.. seats.Select(p => (p.Seat, p.UserId))];
        }
    }

    [Fact]
    public async Task Post_QuotaOfSeatsInTheLastDayReached_Returns429DailyQuotaReached()
    {
        var me = await GamesApi.NewPlayerAsync(Client, Ct);
        var opponent = await GamesApi.NewPlayerAsync(Client, Ct);
        await GamesApi.SeedFinishedGamesAsync(
            Fixture, opponent.UserId, me.UserId, GameErrors.MaxGamesJoinedPerWindow, DateTimeOffset.UtcNow.AddHours(-1), Ct);
        var lobby = await GamesApi.CreateGameAsync(Client, await GamesApi.NewPlayerAsync(Client, Ct), Ct);

        var response = await GamesApi.JoinAsync(Client, me.AccessToken, lobby.InviteCode, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "game.daily_quota_reached", Ct);
    }

    [Fact]
    public async Task Post_LobbyCancelledDuringTheJoin_Returns404InviteNotFound()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        // The cancel deletes the lobby under the join's seat insert, which then fails on the foreign key.
        var (cancel, join) = await GameRowLock.RaceInOrderAsync(
            Fixture.ConnectionString,
            game.Id,
            () => GamesApi.CancelAsync(Client, creator.AccessToken, game.Id, Ct),
            () => GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct),
            Ct);

        cancel.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await join.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.invite_not_found", Ct);
    }
}
