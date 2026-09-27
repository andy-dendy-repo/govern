using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Govern.Application.Games.Contracts;
using Govern.Application.Users.Contracts;
using Govern.Core.Serialization;

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
