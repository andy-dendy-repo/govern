using Govern.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Govern.Infrastructure.Persistence;

/// <summary>
/// Adapts an EF Core transaction to <see cref="IApplicationDbContextTransaction"/>.
/// </summary>
internal sealed class ApplicationDbContextTransaction(IDbContextTransaction transaction) : IApplicationDbContextTransaction
{
    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
