using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Govern.Application.Games.Contracts;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Domain.Entities;
using Govern.Domain.ValueObjects;
using Govern.Domain.Enums;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Govern.IntegrationTests.Features.Games;

public class StartGameEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_CreatorWithFullLobby_Returns200WithCallersViewAndPersistsState()
    {
        var (game, creator, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await GamesApi.ReadGameAsync(response, Ct);
        body.Phase.ShouldBe(GamePhase.InProgress);
        body.InviteCode.ShouldBeNull();
        var me = body.Players.Single(p => p.IsYou);
        me.Seat.ShouldBe(0);
        body.View.ShouldNotBeNull().Viewer.ShouldBe(new PlayerId(me.PlayerId));
        body.View.CurrentPlayer.ShouldBe(new PlayerId(me.PlayerId));

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var stored = await db.Games.AsNoTracking().Include(g => g.Players).SingleAsync(g => g.Id == game.Id, Ct);
            stored.Phase.ShouldBe(GamePhase.InProgress);
            stored.Revision.ShouldBe(2);
            var seats = stored.Players.OrderBy(p => p.Seat).Select(p => new PlayerId(p.Id));
            stored.State.ShouldNotBeNull().Players.Select(p => p.Id).ShouldBe(seats);
        }
    }

    [Theory]
    [InlineData("basin", "basin", 32, 20)]
    [InlineData("random", "basin", 32, 20)]
    [InlineData("generated", "generated", 20, 14)]
    public async Task Post_LobbyWithAMapChoice_StartsOnThatMap(string choice, string started, int width, int height)
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var created = await GamesApi.CreateWithMapAsync(Client, creator.AccessToken, choice, Ct);
        var game = (await created.Content.ReadFromJsonAsync<CreateGameResponse>(GamesApi.Json, Ct))!;
        await GamesApi.JoinAsync(Client, joiner.AccessToken, game.InviteCode, Ct);

        var body = await GamesApi.ReadGameAsync(await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct), Ct);

        body.Map.ShouldBe(started);
        body.View.ShouldNotBeNull().Width.ShouldBe(width);
        body.View.Height.ShouldBe(height);
    }

    [Fact]
    public async Task Post_LobbyWhoseMapLeftThePool_Returns409()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var joiner = await GamesApi.NewPlayerAsync(Client, Ct);
        var lobby = Game.Create(creator.UserId, DateTimeOffset.UtcNow, MapChoice.Create("retired_map"));
        lobby.Join(joiner.UserId, DateTimeOffset.UtcNow);
        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            db.Games.Add(lobby);
            await db.SaveChangesAsync(Ct);
        }

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, lobby.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.map_not_in_pool", Ct);
    }

    [Fact]
    public async Task Post_Started_StoresStateAsJsonbObjectInTheSerializerFormat()
    {
        var (game, _, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT pg_typeof(state)::text, jsonb_typeof(state), state::text FROM games WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", game.Id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        (await reader.ReadAsync(Ct)).ShouldBeTrue();

        reader.GetString(0).ShouldBe("jsonb");
        reader.GetString(1).ShouldBe("object");
        var stored = reader.GetString(2);
        var state = GameSerializer.DeserializeState(stored);
        state.SchemaVersion.ShouldBe(GameState.CurrentSchemaVersion);
        state.Players.Length.ShouldBe(2);

        // Lossless round trip (RngState is a full 64-bit value that later commands depend on).
        using var storedJson = JsonDocument.Parse(stored);
        using var reserialized = JsonDocument.Parse(GameSerializer.Serialize(state));
        JsonElement.DeepEquals(storedJson.RootElement, reserialized.RootElement).ShouldBeTrue();
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, accessToken: null, game.Id, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_PlayerWhoIsNotTheCreator_Returns403()
    {
        var (game, _, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, joiner.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "game.not_creator", Ct);
    }

    [Fact]
    public async Task Post_Outsider_Returns404()
    {
        var (game, _, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        var outsider = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, outsider.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_UnknownGame_Returns404()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, Guid.CreateVersion7(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "game.not_found", Ct);
    }

    [Fact]
    public async Task Post_EmptyGameId_Returns400()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, Guid.Empty, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "GameId");
    }

    [Fact]
    public async Task Post_OnlyCreatorSeated_Returns409()
    {
        var creator = await GamesApi.NewPlayerAsync(Client, Ct);
        var game = await GamesApi.CreateGameAsync(Client, creator, Ct);

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.not_enough_players", Ct);
    }

    [Fact]
    public async Task Post_AlreadyStarted_Returns409()
    {
        var (game, creator, _) = await GamesApi.CreateStartedGameAsync(Client, Ct);

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
    }

    [Fact]
    public async Task Post_SeveralTimesConcurrently_StartsOnce()
    {
        var (game, creator, _) = await GamesApi.CreateFullLobbyAsync(Client, Ct);

        var responses = await GameRowLock.RaceAsync(Fixture.ConnectionString, game.Id, 5, () => Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct))), Ct);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            await rejected.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.already_started", Ct);
        }
    }

    [Fact]
    public async Task Post_AfterTheJoinerLeft_Returns409NotEnoughPlayers()
    {
        var (game, creator, joiner) = await GamesApi.CreateFullLobbyAsync(Client, Ct);
        (await GamesApi.LeaveAsync(Client, joiner.AccessToken, game.Id, Ct)).EnsureSuccessStatusCode();

        var response = await GamesApi.StartAsync(Client, creator.AccessToken, game.Id, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "game.not_enough_players", Ct);
    }
}
