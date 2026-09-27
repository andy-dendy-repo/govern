---
paths:
  - "tests/Govern.ArchitectureTests/**"
---

# Architecture tests

They enforce the rules in `.claude/rules/architecture.md` and `project-map.md`. **Every architecture convention must have a test here**, and a convention change updates the rule file and the test in the same change.

## Structure

| File | Enforces | Technique |
|---|---|---|
| `Conventions/Layers.cs` | Known projects + allowed references (single source of truth) | data |
| `Conventions/Templates.cs` | UF / AGFT / 3DFT folder specs per project (single source of truth) | data (`FolderSpec`) |
| `ProjectReferenceTests` | csproj `ProjectReference`s match `Layers`; every `src/` project is registered; `Govern.UnitTests` references only `Layers.UnitTestReferences` (Domain, Utils) | XML scan of `.csproj` |
| `FolderStructureTests` | Allowed folders/root files; `Contracts/*Contract.cs` contain only prefixed Request/Response/Validator/Handler (+ `file` types); one endpoint per `Features` file | filesystem + source scan (`Support/TypeDeclarations`) |
| `LayerDependencyTests` | Type-level dependencies between layers; Domain free of EF Core / ASP.NET Core; Application free of ASP.NET Core; `Api.Features` free of Infrastructure / Domain; no mapping libraries | ArchUnitNET |
| `CodeMetricsTests` | Size limits from `Conventions/CodeLimits.cs`: file lines, member body lines, method and constructor parameters (generated code and Core excluded) | Roslyn syntax parse of `src/**/*.cs` |
| `NamingConventionTests` | No `Helper` suffix; suffix per folder; contract types; sealed `IRequestHandler` handlers; static `*Endpoint` classes with `Map(IEndpointRouteBuilder)`; entities derive from `BaseEntity`, events from `BaseEvent`; value objects are sealed, immutable and not entities; sealed `INotificationHandler` event handlers; `I` prefix; Abstractions abstract; Extensions static; Enums only enums | ArchUnitNET |

`Support/GovernArchitecture` loads the Govern assemblies once (all layered projects except Client and Core). `Support/SourceTree` finds the repo root (`Govern.slnx`) and skips `bin/`, `obj/`, `Properties/`, `wwwroot/` and dot-folders.

## Writing rules

- Use ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3`): `Types()/Classes()/Interfaces().That()…Should()…Because("why")`.
  - Always add `.WithoutRequiringPositiveResults()`, because the template grows on demand and rules must pass while a folder is still empty.
  - Always add `.Because(…)` with the reason, which shows in the failure message.
- Match namespaces with `ResideInNamespaceMatching(regex)`. Build project patterns with `Layers.NamespacePattern(project)` so `Govern.Application` does not match `Govern.Application.Common`.
- Exclude nested and compiler-generated types from naming rules (`AreNotNested()`, `DoNotHaveNameContaining("<")`); `file` types compile to `<File>…` names.
- For things the compiled model can't see (folders, file names, file-level declarations), use `SourceTree` + a filesystem scan and report **all** violations with repo-relative paths.
- Name the class `<Area>Tests` and each test `Subject_Rule`, e.g. `Types_DoNotUseHelperSuffix`, `Layer_DoesNotDependOnForbiddenLayers`.

## Verifying a new rule

Before trusting a new rule, prove it can fail:
1. Add a deliberately violating file under `src/`.
2. Run `dotnet test --project tests/Govern.ArchitectureTests` and see it fail with a readable message.
3. Delete the file and see it pass.
