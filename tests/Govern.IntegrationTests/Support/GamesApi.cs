using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Govern.Api.Models;
using Govern.Application.Games.Contracts;
using Govern.Application.Users.Contracts;
using Govern.Core.Engine;
using Govern.Core.Serialization;
using Govern.Domain.Entities;
using Govern.IntegrationTests.Fixtures;

namespace Govern.IntegrationTests.Support;

/// <summary>
/// Thin helpers over the Games endpoints for arranging test state through the public HTTP API.
/// </summary>
public static class GamesApi
{
    /// <summary>The Api writes enums as camelCase strings, exactly like <see cref="GameSerializer"/>.</summary>
    public static JsonSerializerOptions Json => GameSerializer.Options;

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string? accessToken, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Post, "/api/games", accessToken, content: null, ct);

    public static Task<HttpResponseMessage> JoinAsync(HttpClient client, string? accessToken, string? inviteCode, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Post, "/api/games/join", accessToken, JsonContent.Create(new { inviteCode }), ct);

    public static Task<HttpResponseMessage> StartAsync(HttpClient client, string? accessToken, Guid gameId, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Post, $"/api/games/{gameId}/start", accessToken, content: null, ct);

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string? accessToken, Guid gameId, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Get, $"/api/games/{gameId}", accessToken, content: null, ct);

    public static Task<HttpResponseMessage> ListAsync(HttpClient client, string? accessToken, string query, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Get, $"/api/games{query}", accessToken, content: null, ct);

    public static Task<HttpResponseMessage> SendCommandAsync(
        HttpClient client, string? accessToken, Guid gameId, long expectedVersion, GameCommand command, CancellationToken ct) =>
        SendCommandJsonAsync(client, accessToken, gameId, GameSerializer.Serialize(new GameCommandBody(expectedVersion, command)), ct);

    public static Task<HttpResponseMessage> SendCommandJsonAsync(
        HttpClient client, string? accessToken, Guid gameId, string json, CancellationToken ct, string mediaType = "application/json") =>
        SendAsync(client, HttpMethod.Post, $"/api/games/{gameId}/commands", accessToken, new StringContent(json, Encoding.UTF8, mediaType), ct);

    public static Task<HttpResponseMessage> SendCommandContentAsync(
        HttpClient client, string? accessToken, Guid gameId, HttpContent content, CancellationToken ct) =>
        SendAsync(client, HttpMethod.Post, $"/api/games/{gameId}/commands", accessToken, content, ct);

    /// <summary>Reads the game as <paramref name="player"/>.</summary>
    public static async Task<GetGameResponse> GetGameAsync(HttpClient client, Player player, Guid gameId, CancellationToken ct)
    {
        var response = await GetAsync(client, player.AccessToken, gameId, ct);
        response.EnsureSuccessStatusCode();
        return await ReadGameAsync(response, ct);
    }

    /// <summary>Signs up and signs in a new user, returning the access token and the user id.</summary>
    public static async Task<Player> NewPlayerAsync(HttpClient client, CancellationToken ct)
    {
        var (_, tokens) = await UsersApi.CreateSignedInUserAsync(client, ct);
        var me = await UsersApi.GetMeAsync(client, tokens.AccessToken, ct);
        me.EnsureSuccessStatusCode();
        var user = (await me.Content.ReadFromJsonAsync<GetCurrentUserResponse>(ct))!;
        return new Player(user.Id, tokens.AccessToken);
    }

    /// <summary>Creates a lobby as <paramref name="creator"/>.</summary>
    public static async Task<CreateGameResponse> CreateGameAsync(HttpClient client, Player creator, CancellationToken ct)
    {
        var response = await CreateAsync(client, creator.AccessToken, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateGameResponse>(Json, ct))!;
    }

    /// <summary>Creates a lobby and has a second new user join it.</summary>
    public static async Task<(CreateGameResponse Game, Player Creator, Player Joiner)> CreateFullLobbyAsync(HttpClient client, CancellationToken ct)
    {
        var creator = await NewPlayerAsync(client, ct);
        var joiner = await NewPlayerAsync(client, ct);
        var game = await CreateGameAsync(client, creator, ct);
        (await JoinAsync(client, joiner.AccessToken, game.InviteCode, ct)).EnsureSuccessStatusCode();
        return (game, creator, joiner);
    }

    /// <summary>Creates, fills and starts a game.</summary>
    public static async Task<(CreateGameResponse Game, Player Creator, Player Joiner)> CreateStartedGameAsync(HttpClient client, CancellationToken ct)
    {
        var lobby = await CreateFullLobbyAsync(client, ct);
        (await StartAsync(client, lobby.Creator.AccessToken, lobby.Game.Id, ct)).EnsureSuccessStatusCode();
        return lobby;
    }

    /// <summary>
    /// Stores <paramref name="count"/> lobbies created by <paramref name="creatorId"/> and joined by <paramref name="userId"/>
    /// directly in the database: unfinished games for the user, without going through the per-creator lobby cap.
    /// </summary>
    public static async Task SeedJoinedLobbiesAsync(GovernApiFixture fixture, Guid creatorId, Guid userId, int count, CancellationToken ct)
    {
        var (scope, db) = await fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            for (var i = 0; i < count; i++)
            {
                var game = Game.Create(creatorId, DateTimeOffset.UtcNow);
                game.Join(userId, DateTimeOffset.UtcNow);
                db.Games.Add(game);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task<GetGameResponse> ReadGameAsync(HttpResponseMessage response, CancellationToken ct) =>
        (await response.Content.ReadFromJsonAsync<GetGameResponse>(Json, ct))!;

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string uri, string? accessToken, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request, ct);
    }

    /// <summary>A signed-in test user.</summary>
    public sealed record Player(Guid UserId, string AccessToken);
}
