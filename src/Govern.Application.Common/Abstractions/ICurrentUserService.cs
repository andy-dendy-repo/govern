namespace Govern.Application.Common.Abstractions;

/// <summary>
/// The authenticated caller of the current request.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The caller's user id, or <see langword="null"/> when the request is anonymous.
    /// </summary>
    Guid? UserId { get; }
}
