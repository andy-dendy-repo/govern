---
title: "ADR 0003: no mapping libraries; EF projections and static members on target types"
tags: [adr, mapping, ef-core, projections, dto]
code: [src/Govern.Application/Users/Contracts/GetCurrentUserContract.cs, tests/Govern.ArchitectureTests/LayerDependencyTests.cs]
updated: 2026-09-27
---

# ADR 0003: no mapping libraries

## Status
Accepted (2026-09-27).

## Context
Mapping libraries (AutoMapper, Mapster, Mapperly) hide data flow behind configuration or generated code. They make it easy to over-fetch entities, and they fail at runtime or produce SQL you didn't expect.

## Decision
- No mapping libraries. `LayerDependencyTests.Types_DoNotUseMappingLibraries` enforces this.
- **Reads** project in the query with `Select(...)` into the response, together with `AsNoTracking()`. The projection is inline by default.
- **Reused projections** live on the target type as `public static Expression<Func<TSource, TTarget>> Projection`, plus `From(...)` compiled from it for in-memory mapping. Example: `GetCurrentUserResponse.Projection`.
- **Writes** go through domain factories and methods, never property copying.

## Consequences
- Queries read only the needed columns, and the SQL is predictable.
- There is a little more hand-written code, but every mapping is visible, debuggable and refactor-safe.
- Details: `.claude/rules/mapping.md`.
