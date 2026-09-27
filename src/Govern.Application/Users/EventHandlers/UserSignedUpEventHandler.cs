using Govern.Application.Common.Abstractions;
using Govern.Application.Common.Models;
using Govern.Domain.Events;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Application.Users.EventHandlers;

/// <summary>
/// Sends the welcome email after a user signs up.
/// </summary>
internal sealed class UserSignedUpEventHandler(IEmailService emailService) : INotificationHandler<UserSignedUpEvent>
{
    /// <summary>Subject of the welcome email.</summary>
    public const string WelcomeSubject = "Welcome to Govern";

    /// <inheritdoc />
    public async ValueTask HandleAsync(UserSignedUpEvent notification, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(notification.Email.Value, WelcomeSubject, "Your Govern account has been created.");
        await emailService.SendAsync(message, cancellationToken);
    }
}
