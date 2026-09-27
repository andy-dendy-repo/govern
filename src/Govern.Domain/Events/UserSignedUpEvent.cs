using Govern.Domain.EntityBase;
using Govern.Domain.ValueObjects;

namespace Govern.Domain.Events;

/// <summary>
/// Raised when a new user account is created.
/// </summary>
/// <param name="UserId">Identifier of the new user.</param>
/// <param name="Email">Email address of the new user.</param>
public sealed record UserSignedUpEvent(Guid UserId, Email Email) : BaseEvent;
