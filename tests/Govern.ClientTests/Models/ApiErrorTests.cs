using System.Net;
using System.Text;
using Govern.Client.Models;

namespace Govern.ClientTests.Models;

public class ApiErrorTests
{
    [Fact]
    public async Task ReadAsync_ProblemDetails_ReadsCodeAndTitle()
    {
        var error = await ReadAsync(HttpStatusCode.Conflict, """{"title":"An account with this email already exists.","status":409,"code":"user.email_taken"}""");

        error.ShouldBe(error with { Status = 409, Code = "user.email_taken", Message = "An account with this email already exists." });
        error.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_ValidationProblem_ReadsFieldErrors()
    {
        var error = await ReadAsync(HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","code":"validation","errors":{"Password":["Too short."]}}""");

        error.FieldErrors.ShouldContainKey("Password");
        error.FieldErrors["Password"].ShouldBe(["Too short."]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many requests. Wait a minute and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "Something went wrong on the server. Try again later.")]
    [InlineData(HttpStatusCode.NotFound, "Not found.")]
    public async Task ReadAsync_EmptyBody_UsesTheMessageForTheStatus(HttpStatusCode status, string message)
    {
        var error = await ReadAsync(status, string.Empty);

        error.Status.ShouldBe((int)status);
        error.Code.ShouldBeNull();
        error.Message.ShouldBe(message);
    }

    [Theory]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("""{"status":502}""")]
    public async Task ReadAsync_NotAProblemWithATitle_UsesTheMessageForTheStatus(string body)
    {
        var error = await ReadAsync(HttpStatusCode.BadGateway, body);

        error.Message.ShouldBe("Something went wrong on the server. Try again later.");
    }

    [Fact]
    public void Network_IsNetwork()
    {
        ApiError.Network.IsNetwork.ShouldBeTrue();
        ApiError.FromStatus(HttpStatusCode.BadRequest).IsNetwork.ShouldBeFalse();
    }

    private static async Task<ApiError> ReadAsync(HttpStatusCode status, string body)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return await ApiError.ReadAsync(status, stream, Xunit.TestContext.Current.CancellationToken);
    }
}
