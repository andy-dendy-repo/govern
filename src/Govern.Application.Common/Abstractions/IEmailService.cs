using Govern.Application.Common.Models;

namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Sends transactional email.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends <paramref name="message"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A task that completes when the message is accepted for delivery.</returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
