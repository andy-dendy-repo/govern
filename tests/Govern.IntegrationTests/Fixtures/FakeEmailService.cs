using System.Collections.Concurrent;
using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;

namespace Govern.IntegrationTests.Fixtures;

/// <summary>Captures sent emails instead of delivering them.</summary>
public sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();
}
