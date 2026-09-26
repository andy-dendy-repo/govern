using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Common.Abstractions;

/// <summary>
/// The unit of work over the Govern database. Create one per operation through
/// <see cref="IApplicationDbContextFactory"/> and dispose it with <c>await using</c>.
/// </summary>
public interface IApplicationDbContext : IAsyncDisposable
{
    /// <summary>User accounts.</summary>
    DbSet<User> Users { get; }

    /// <summary>Refresh tokens (owned by users).</summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>
    /// Saves all tracked changes; audit timestamps are set and domain events are dispatched after a successful save.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The number of written state entries.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts an explicit transaction for work that spans several saves.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The transaction; roll back is automatic when it is disposed without commit.</returns>
    Task<IApplicationDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
