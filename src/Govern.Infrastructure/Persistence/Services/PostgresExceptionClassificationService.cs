using Govern.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Govern.Infrastructure.Persistence.Services;

/// <summary>
/// Recognises PostgreSQL error codes behind EF Core exceptions.
/// </summary>
internal sealed class PostgresExceptionClassificationService : IDbExceptionClassificationService
{
    /// <inheritdoc />
    public bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
