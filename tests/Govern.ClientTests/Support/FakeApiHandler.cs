using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Govern.Client.Models;

namespace Govern.ClientTests.Support;

/// <summary>
/// A scripted Api for typed clients and the auth session. Responses are queued per method and path; the last response for a
/// route is reused once the queue is down to it. Every request is recorded. An unscripted request throws, failing the test.
/// </summary>
public sealed class FakeApiHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<HttpRequestMessage, Task<HttpResponseMessage>>>> _routes = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    /// <summary>The requests received so far, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    /// <summary>The requests received for <paramref name="path"/>.</summary>
    public IReadOnlyList<RecordedRequest> RequestsTo(string path) => [.. _requests.Where(request => request.Path == path)];

    /// <summary>Queues a response for <paramref name="method"/> <paramref name="path"/> (path and query, e.g. <c>/api/users/me</c>).</summary>
    public FakeApiHandler On(HttpMethod method, string path, Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        _routes.GetOrAdd(Key(method, path), static _ => new()).Enqueue(respond);
        return this;
    }

    /// <summary>Queues a fixed response.</summary>
    public FakeApiHandler On(HttpMethod method, string path, Func<HttpResponseMessage> respond) =>
        On(method, path, _ => Task.FromResult(respond()));

    /// <summary>A JSON response serialized like the Api does.</summary>
    public static HttpResponseMessage Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body, options: ApiJson.Options) };

    /// <summary>A ProblemDetails response with the Api's <c>code</c> extension.</summary>
    public static HttpResponseMessage Problem(HttpStatusCode status, string code, string title) =>
        Json(new { type = "about:blank", title, status = (int)status, code }, status);

    /// <summary>A validation ProblemDetails response.</summary>
    public static HttpResponseMessage ValidationProblem(string field, string message) =>
        Json(new { title = "One or more validation errors occurred.", status = 400, code = "validation", errors = new Dictionary<string, string[]> { [field] = [message] } },
            HttpStatusCode.BadRequest);

    /// <summary>A response without a body.</summary>
    public static HttpResponseMessage Empty(HttpStatusCode status) => new(status);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new RecordedRequest(request.Method, path, request.Headers.Authorization?.ToString(), body, request.Headers.Accept.ToString()));

        if (!_routes.TryGetValue(Key(request.Method, path), out var queue) || !TryNext(queue, out var respond))
        {
            throw new InvalidOperationException($"No response scripted for {request.Method} {path}.");
        }

        var response = await respond(request);
        response.RequestMessage = request;
        return response;
    }

    private static bool TryNext(ConcurrentQueue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> queue,
        out Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        lock (queue)
        {
            return queue.Count > 1 ? queue.TryDequeue(out respond!) : queue.TryPeek(out respond!);
        }
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path}";
}

/// <summary>A request the fake Api received.</summary>
public sealed record RecordedRequest(HttpMethod Method, string Path, string? Authorization, string? Body, string Accept = "");
