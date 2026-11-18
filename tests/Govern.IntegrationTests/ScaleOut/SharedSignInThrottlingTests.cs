using System.Net;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;

namespace Govern.IntegrationTests.ScaleOut;

public class SharedSignInThrottlingTests(GovernApiFixture fixture, RedisFixture redis) : ScaleOutTestBase(fixture, redis)
{
    [Fact]
    public async Task FailedAttemptsOnBothInstances_AddUpToTheLockout()
    {
        var a = StartInstance();
        var b = StartInstance();
        var email = await SignUpAsync(a);
        await FailSignInsAsync(a, email, 3);
        await FailSignInsAsync(b, email, 2);

        var response = await UsersApi.SignInAsync(b, email, UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "user.too_many_sign_in_attempts", Ct);
    }

    [Fact]
    public async Task ParallelWrongPasswordsOnBothInstances_OnlyMaxFailedAttemptsAreChecked()
    {
        var a = StartInstance();
        var b = StartInstance();
        var email = await SignUpAsync(a);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(i => UsersApi.SignInAsync(i % 2 == 0 ? a : b, email, "wrong-password", Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(5);
        responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests).ShouldBe(15);
    }

    [Fact]
    public async Task SuccessfulSignInOnOneInstance_ResetsTheAttemptsOnTheOther()
    {
        var a = StartInstance();
        var b = StartInstance();
        var email = await SignUpAsync(a);
        await FailSignInsAsync(a, email, 4);
        (await UsersApi.SignInAsync(b, email, UsersApi.Password, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await FailSignInsAsync(a, email, 4);

        var response = await UsersApi.SignInAsync(a, email, UsersApi.Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string> SignUpAsync(HttpClient client)
    {
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();
        return email;
    }

    private static async Task FailSignInsAsync(HttpClient client, string email, int count)
    {
        for (var i = 0; i < count; i++)
        {
            (await UsersApi.SignInAsync(client, email, "wrong-password", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
