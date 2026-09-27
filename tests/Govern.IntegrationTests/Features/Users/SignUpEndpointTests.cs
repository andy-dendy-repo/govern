using System.Net;
using System.Net.Http.Json;
using Govern.Application.Users.Contracts;
using Govern.IntegrationTests.Fixtures;
using Govern.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Govern.IntegrationTests.Features.Users;

public class SignUpEndpointTests(GovernApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Post_ValidRequest_Returns201PersistsHashedUserAndSendsWelcomeEmail()
    {
        var email = UsersApi.NewEmail();

        var response = await UsersApi.SignUpAsync(Client, email.ToUpperInvariant(), UsersApi.Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.ToString().ShouldBe("/api/users/me");
        var body = await response.Content.ReadFromJsonAsync<SignUpResponse>(Ct);

        var (scope, db) = await Fixture.OpenDbAsync();
        await using (scope)
        await using (db)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == body!.UserId, Ct);
            user.Email.Value.ShouldBe(email);
            user.PasswordHash.ShouldNotContain(UsersApi.Password);
            user.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        var welcome = Fixture.Factory.Emails.Sent.ShouldHaveSingleItem();
        welcome.To.ShouldBe(email);
        welcome.Subject.ShouldBe("Welcome to Govern");
    }

    [Fact]
    public async Task Post_EmailAlreadyRegisteredInDifferentCase_Returns409()
    {
        var email = UsersApi.NewEmail();
        (await UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct)).EnsureSuccessStatusCode();

        var response = await UsersApi.SignUpAsync(Client, $"  {email.ToUpperInvariant()} ", UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.email_taken", Ct);
    }

    [Fact]
    public async Task Post_SameEmailConcurrently_CreatesOneAccountAndReturns409ForOthers()
    {
        var email = UsersApi.NewEmail();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.Created).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_InvalidEmailAndShortPassword_Returns400WithFieldErrors()
    {
        var response = await UsersApi.SignUpAsync(Client, "not-an-email", "short", Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Email", "Password");
        Fixture.Factory.Emails.Sent.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("user\r\nBcc: victim@example.com@example.com")]
    [InlineData("us er@example.com")]
    [InlineData("user\u200B@example.com")]
    public async Task Post_EmailWithUnsafeCharacters_Returns400(string email)
    {
        var response = await UsersApi.SignUpAsync(Client, email, UsersApi.Password, Ct);

        await response.ShouldBeValidationProblemAsync(Ct, "Email");
        Fixture.Factory.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_LookalikeInDifferentUnicodeComposition_Returns409()
    {
        var local = $"e\u0301-{Guid.NewGuid():N}";
        (await UsersApi.SignUpAsync(Client, $"{local}@example.com", UsersApi.Password, Ct)).EnsureSuccessStatusCode();

        var composed = $"\u00E9{local[2..]}@example.com";
        var response = await UsersApi.SignUpAsync(Client, composed, UsersApi.Password, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.email_taken", Ct);
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400()
    {
        using var content = new StringContent("{\"email\":", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/users/sign-up", content, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code: null, Ct);
    }
}
