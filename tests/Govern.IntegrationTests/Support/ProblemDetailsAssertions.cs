using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Govern.IntegrationTests.Support;

/// <summary>
/// Assertions for RFC 9457 problem responses produced by the Api.
/// </summary>
public static class ProblemDetailsAssertions
{
    /// <summary>Asserts the status and the stable error <c>code</c> extension.</summary>
    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code, CancellationToken ct)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe((int)status);

        if (code is not null)
        {
            problem.Extensions["code"]?.ToString().ShouldBe(code);
        }
    }

    /// <summary>Asserts a 400 validation problem listing exactly the given invalid fields.</summary>
    public static async Task ShouldBeValidationProblemAsync(this HttpResponseMessage response, CancellationToken ct, params string[] fields)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(fields, ignoreOrder: true);
    }
}
