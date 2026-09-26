using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Govern.Infrastructure.Emailing.Services;

/// <summary>
/// Mock email delivery: logs that a message would be sent (subject only, no recipient or body) and drops it.
/// Replace with a real provider under <c>Emailing/</c> when email delivery is needed.
/// </summary>
internal sealed partial class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Log.EmailNotSent(logger, message.Subject);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Email '{Subject}' was not sent: email delivery is mocked")]
        public static partial void EmailNotSent(ILogger logger, string subject);
    }
}
