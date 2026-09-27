using Govern.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Govern.Infrastructure.Persistence;

/// <summary>
/// Creates <see cref="ApplicationDbContext"/> instances through EF's scoped <see cref="IDbContextFactory{TContext}"/>,
/// so interceptors can depend on scoped services such as the mediator.
/// </summary>
internal sealed class ApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IApplicationDbContextFactory
{
    /// <inheritdoc />
    public async Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken) =>
        await dbContextFactory.CreateDbContextAsync(cancellationToken);
}
