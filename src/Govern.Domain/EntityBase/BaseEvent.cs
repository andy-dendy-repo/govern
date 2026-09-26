using Govern.Utils.Mediator.Abstractions;

namespace Govern.Domain.EntityBase;

/// <summary>
/// Base for domain events. They are published through the mediator after the raising entity is saved and are
/// handled by <c>EventHandlers</c> in the Application layer.
/// </summary>
public abstract record BaseEvent : INotification;
