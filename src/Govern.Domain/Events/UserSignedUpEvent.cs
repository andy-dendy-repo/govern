using Govern.Domain.EntityBase;

namespace Govern.Domain.Events;

/// <summary>
/// Raised when a new user account is created.
/// </summary>
/// <param name="UserId">Identifier of the new user.</param>
/// <param name="Email">Normalised email address of the new user.</param>
public sealed record UserSignedUpEvent(Guid UserId, string Email) : BaseEvent;
