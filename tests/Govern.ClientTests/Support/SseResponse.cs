using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Govern.Client.Models;

namespace Govern.ClientTests.Support;

/// <summary>
/// An open <c>text/event-stream</c> response for <see cref="FakeApiHandler"/>: the body is a pipe the test writes events to,
/// and it stays open until <see cref="Complete"/> or <see cref="Fail"/>. One instance serves one connection.
/// </summary>
public sealed class SseResponse
{
    private readonly Pipe _pipe = new();

    public SseResponse()
    {
        var content = new StreamContent(_pipe.Reader.AsStream());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        Response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    /// <summary>The response to return from <see cref="FakeApiHandler.On(HttpMethod, string, Func{HttpResponseMessage})"/>.</summary>
    public HttpResponseMessage Response { get; }

    /// <summary>Writes a <c>game</c> event with <paramref name="game"/> as JSON, like the Api.</summary>
    public Task WriteGameAsync(object game) => WriteRawAsync("game", JsonSerializer.Serialize(game, ApiJson.Options));

    /// <summary>Writes a <c>heartbeat</c> event.</summary>
    public Task WriteHeartbeatAsync() => WriteRawAsync("heartbeat", "null");

    /// <summary>Writes an event of any type with raw data.</summary>
    public async Task WriteRawAsync(string eventType, string data)
    {
        await _pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes($"event: {eventType}\ndata: {data}\n\n"));
        await _pipe.Writer.FlushAsync();
    }

    /// <summary>Ends the stream normally, like the Api at token expiry or after the finished snapshot.</summary>
    public void Complete() => _pipe.Writer.Complete();

    /// <summary>Breaks the connection: the next read throws an <see cref="IOException"/>.</summary>
    public void Fail() => _pipe.Writer.Complete(new IOException("The connection was reset."));
}
