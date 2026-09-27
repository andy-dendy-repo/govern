---
paths:
  - "src/**/*.cs"
---

# Mapping

**No mapping libraries.** AutoMapper, Mapster and Mapperly are banned (`LayerDependencyTests.Types_DoNotUseMappingLibraries`). Mapping is plain C#, so it's visible, debuggable and translatable by EF Core.

## Reads: project in the query

Project straight into the response or model inside the query, with `AsNoTracking()`. This reads only the needed columns and never loads entities you don't modify.

```csharp
var orders = await db.Orders
    .AsNoTracking()
    .Where(order => order.OwnerId == userId)
    .Select(order => new OrderSummary(order.Id, order.Title, order.Lines.Count))
    .ToListAsync(cancellationToken);
```

The default is to project **inline** in the handler. A projection used by one use case belongs with that use case.

## Reuse: the target type owns its mapping

When the same projection is needed in a second place, move it to a static member **on the target type**: the response or model it produces. The shape and its mapping then change together.

```csharp
public sealed record GetCurrentUserResponse(Guid Id, string Email, DateTimeOffset CreatedAt) : IResponse
{
    /// <summary>EF Core projection from <see cref="User"/>.</summary>
    public static Expression<Func<User, GetCurrentUserResponse>> Projection { get; } =
        user => new GetCurrentUserResponse(user.Id, user.Email.Value, user.CreatedAt);
}

// handler
.Select(GetCurrentUserResponse.Projection)
```

- If the same mapping is also needed in memory (an entity you already loaded), add `From` built from the expression so there is one source of truth:
  ```csharp
  private static readonly Func<User, GetCurrentUserResponse> Compiled = Projection.Compile();
  public static GetCurrentUserResponse From(User user) => Compiled(user);
  ```
- For mapping from non-entity values (e.g. an issued token pair), use a plain `public static XxxResponse From(Source source)` on the target (see `SignInResponse.From`).
- Rare case: a complex mapping that combines several sources and is used by several features goes in a static `XxxMapper` class in `[Feature]/Helpers/`. `Mapper` is an allowed suffix; `Helper` never is.

## Writes: through the domain

Request → entity is **never** property-by-property copying, which risks mass assignment. Create and change entities through domain factories and methods that enforce invariants: `User.SignUp(email, passwordHash)`, `user.RotateRefreshToken(...)`.

## Don'ts

- Don't map in Domain. Entities don't know about responses.
- Don't use `ToDto()` extension methods on entities inside `IQueryable`. They can't be translated to SQL: EF either fails or loads whole entities.
- Don't return entities from handlers or endpoints.
- Don't add Api-level DTOs duplicating Application responses. Endpoints return the Application response as-is, unless the HTTP contract must differ.
