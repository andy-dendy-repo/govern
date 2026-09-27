using System.Linq.Expressions;
using Govern.Application.Common.Abstractions;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Users.Contracts;

/// <summary>
/// Returns the authenticated caller's account ("who am I").
/// </summary>
public sealed record GetCurrentUserRequest : IRequest<GetCurrentUserRequest, GetCurrentUserResponse>;

/// <summary>
/// The caller's account.
/// </summary>
/// <param name="Id">User id.</param>
/// <param name="Email">Normalised email address.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record GetCurrentUserResponse(Guid Id, string Email, DateTimeOffset CreatedAt) : IResponse
{
    /// <summary>
    /// EF Core projection from <see cref="User"/>; translated to SQL so only these columns are read.
    /// </summary>
    public static Expression<Func<User, GetCurrentUserResponse>> Projection { get; } =
        user => new GetCurrentUserResponse(user.Id, user.Email, user.CreatedAt);
}

/// <summary>
/// Handles <see cref="GetCurrentUserRequest"/> for the user in <see cref="ICurrentUserService"/>.
/// </summary>
internal sealed class GetCurrentUserHandler(IApplicationDbContextFactory dbContextFactory, ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserRequest, GetCurrentUserResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<GetCurrentUserResponse>> HandleAsync(GetCurrentUserRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var response = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(GetCurrentUserResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null)
        {
            return UserErrors.NotFound;
        }

        return response;
    }
}
