using System.Net.Http.Headers;
using System.Net.Http.Json;
using Govern.Application.Users.Contracts;

namespace Govern.IntegrationTests.Support;

/// <summary>
/// Thin helpers over the Users endpoints for arranging test state through the public HTTP API.
/// </summary>
public static class UsersApi
{
    public const string Password = "Correct-horse-9";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> SignUpAsync(HttpClient client, string email, string password, CancellationToken ct) =>
        client.PostAsJsonAsync("/api/users/sign-up", new { email, password }, ct);

    public static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password, CancellationToken ct) =>
        client.PostAsJsonAsync("/api/users/sign-in", new { email, password }, ct);

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken, CancellationToken ct) =>
        client.PostAsJsonAsync("/api/users/refresh", new { refreshToken }, ct);

    public static async Task<HttpResponseMessage> SignOutAsync(HttpClient client, string? accessToken, string refreshToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sign-out") { Content = JsonContent.Create(new { refreshToken }) };
        Authorize(request, accessToken);
        return await client.SendAsync(request, ct);
    }

    public static async Task<HttpResponseMessage> SignOutEverywhereAsync(HttpClient client, string? accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/sign-out-everywhere");
        Authorize(request, accessToken);
        return await client.SendAsync(request, ct);
    }

    public static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string? accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        Authorize(request, accessToken);
        return await client.SendAsync(request, ct);
    }

    /// <summary>Signs an existing user in and returns the issued tokens.</summary>
    public static async Task<SignInResponse> SignInTokensAsync(HttpClient client, string email, CancellationToken ct)
    {
        var signIn = await SignInAsync(client, email, Password, ct);
        signIn.EnsureSuccessStatusCode();
        return (await signIn.Content.ReadFromJsonAsync<SignInResponse>(ct))!;
    }

    /// <summary>Signs up a new user and signs in, returning the email and the issued tokens.</summary>
    public static async Task<(string Email, SignInResponse Tokens)> CreateSignedInUserAsync(HttpClient client, CancellationToken ct)
    {
        var email = NewEmail();
        (await SignUpAsync(client, email, Password, ct)).EnsureSuccessStatusCode();

        return (email, await SignInTokensAsync(client, email, ct));
    }

    private static void Authorize(HttpRequestMessage request, string? accessToken)
    {
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}
