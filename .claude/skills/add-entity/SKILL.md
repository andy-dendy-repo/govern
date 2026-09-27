---
name: add-entity
description: >
  Adds a Govern domain entity with its EF Core configuration, DbSet and migration (and optional domain event).
  Use when a feature needs a new table/aggregate or child entity ("store X", "add an Order entity").
---

# Add an entity

Run inside `/develop-feature`. Reference implementation: `src/Govern.Domain/Entities/User.cs` (aggregate root) and `RefreshToken.cs` (child created only through the root), `src/Govern.Infrastructure/Persistence/Configurations/UserConfiguration.cs`.

Read first: `persistence.md`, `mapping.md`, and `architecture.md` (the Domain template).

## 1. Domain: `src/Govern.Domain/Entities/[Name].cs`
- Derive from `BaseAuditableEntity` (or `BaseEntity` when timestamps make no sense). The Guid v7 `Id` comes from the base.
- `sealed class` with a `private` parameterless constructor for EF and `private set` properties. Initialise strings to `string.Empty`.
- **Creation:** a static factory (`public static Order Create(...)`) that validates invariants with guard clauses and raises a domain event if something should react.
- **Changes:** go through intention-revealing methods (`Rename`, `Close`). Keep children in `private readonly List<T>` exposed as `IReadOnlyCollection<T>`; children are created and changed only through the root (use `internal` members on the child).
- No EF, ASP.NET or DTO types in Domain (arch test).
- **Optional event:** `src/Govern.Domain/Events/[Name][Happened]Event.cs` as a `sealed record ... : BaseEvent`, with a handler in `src/Govern.Application/[Feature]/EventHandlers/[Event]Handler.cs` (`internal sealed`, `INotificationHandler<TEvent>`).

## 2. Persistence
1. `src/Govern.Application.Common/Abstractions/IApplicationDbContext.cs`: add a `DbSet<[Name]> [Names] { get; }` with an XML summary.
2. `src/Govern.Infrastructure/Persistence/ApplicationDbContext.cs`: add `public DbSet<[Name]> [Names] => Set<[Name]>();`.
3. `src/Govern.Infrastructure/Persistence/Configurations/[Name]Configuration.cs`: an `internal sealed` class implementing `IEntityTypeConfiguration<[Name]>`.
   - The key.
   - A max length on every string.
   - Required columns.
   - Unique indexes for business keys.
   - Foreign keys with explicit `OnDelete`, and an index on each.
   - Backing-field navigations with `UsePropertyAccessMode(PropertyAccessMode.Field)`.
4. Run `/add-migration` with a name like `Add[Names]`.

## 3. Tests and docs
- Integration tests exercise the entity through the endpoints of the use cases that need it (`/add-use-case`). There are no unit tests for Domain or Application.
- Document the entity (fields, invariants, events) in `docs/features/[feature].md`.
- Optionally run the `ef-core-specialist` agent on the change.

## 4. Check
Run `dotnet build Govern.slnx`, then `dotnet test Govern.slnx`. The architecture tests check the base type, the folder, the naming and the Domain dependencies.
