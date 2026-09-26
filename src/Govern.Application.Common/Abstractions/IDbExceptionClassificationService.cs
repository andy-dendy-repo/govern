namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Classifies database exceptions without exposing the provider (Npgsql) to the Application layer, so handlers can
/// turn expected races (e.g. two sign-ups with the same email) into errors instead of 500s.
/// </summary>
public interface IDbExceptionClassificationService
{
    /// <summary>
    /// Whether <paramref name="exception"/> was caused by a unique constraint or unique index violation.
    /// </summary>
    /// <param name="exception">The exception thrown by <c>SaveChangesAsync</c>.</param>
    /// <returns><see langword="true"/> for unique violations.</returns>
    bool IsUniqueViolation(Exception exception);
}
