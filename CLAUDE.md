# Govern

Govern is an HTTP Web API with a Blazor WASM client.

| Concern | Choice |
|---|---|
| Runtime | .NET 10 / C# 14 |
| Database | PostgreSQL (EF Core + Npgsql) |
| Auth | JWT bearer tokens in the `Authorization` header |
| UI | Blazor WebAssembly + Radzen |
| Hosting | Azure Web App for Containers (Ubuntu image) for the Api, Azure Static Web App for the Client, cheapest Azure PostgreSQL Flexible Server tier |
| DNS | Cloudflare + Azure |
| Infra as code | Bicep in `infra/` |
| Source control and CI/CD | GitHub + GitHub Actions |

`/Users/andriihome/plaincv/PlainCV` is a **sample only, not a source of truth**. Borrow ideas from it, never conventions. The rules below win.

## Layout

- `src/`: production projects. See `.claude/rules/project-map.md` for what goes where and allowed references.
- `tests/`:
  - `Govern.UnitTests`: Utils only.
  - `Govern.IntegrationTests`: everything else, via HTTP against Postgres in Testcontainers.
  - `Govern.ArchitectureTests`: enforces the rules.
- `docs/`: knowledge base (RAG); index in `docs/README.md`.
- `infra/`: Bicep.
- `.github/workflows/`: CI and release.
- `Govern.Core` is ignored: excluded from all rules. Leave it alone unless asked.

## Commands

```bash
dotnet build Govern.slnx                                        # warnings are errors, XML docs required in src/
dotnet test Govern.slnx                                         # ALL tests (Microsoft Testing Platform, xUnit v3)
dotnet test --project tests/Govern.ArchitectureTests            # one project while iterating
dotnet test Govern.slnx --coverage --coverage-output-format cobertura   # coverage → ./TestResults
dotnet format Govern.slnx --verify-no-changes --exclude src/Govern.Core
dotnet ef migrations add <Name> --project src/Govern.Infrastructure --startup-project src/Govern.Api --output-dir Persistence/Migrations
dotnet tool restore                                             # local tools (cwm-roslyn-navigator, dotnet-ef)
dotnet run --project src/Govern.Api                             # needs user-secrets: see docs/operations/local-development.md
dotnet publish src/Govern.Api -c Release -t:PublishContainer    # container image govern-api:local
az bicep build --file infra/main.bicep                          # compile infra
```

Integration tests need Docker running (Testcontainers starts `postgres:17-alpine`).

## Branching

Branch from `stage` and open the PR to `stage`. A PR from `stage` to `main` is a release; run the `security-auditor` agent first. Critical or High findings block the release. Never commit or push to `main` or `stage` directly; a hook blocks it. Commit or push only when the user asks, using conventional commits.

## Definition of Done (every feature → `/develop-feature`)

1. Planned in the `.claude/rules/planning.md` format.
2. Tests written or updated:
   - unit tests (Utils only)
   - integration tests (happy path, 400, 401/403, domain errors)
   - architecture tests (for new conventions)
3. XML docs on every public member (the build fails otherwise).
4. `docs/` updated (feature doc + index).
5. `.claude` rules, skills and this file corrected if a convention changed. Rules and arch tests change together.
6. **Build and ALL tests have been run in this session and are green, with the summary shown, BEFORE saying the feature is done.** Use `/verify`. A Stop hook blocks finishing while changed code has no green `dotnet test Govern.slnx` run.

## Core conventions (details in `.claude/rules/`)

- **Folder templates:** UF / AGFT / 3DFT per project. Folders are created on demand and are whitelisted by `FolderStructureTests`.
- **No `Helper` suffix.** Name classes by role: Resolver, Mapper, Calculator, Builder…
- **Suffix per folder:** `Options`, `Exception`, `Service`, `Repository`, `EventHandler`, `Endpoint`, and so on.
- **Use case files:** `Application/[Feature]/Contracts/XxxContract.cs` holds `XxxRequest`, `XxxResponse`, `XxxValidator`, `XxxHandler` and `file` types only.
- **Endpoints:** one endpoint per file in `Api/Features/[Feature]/XxxEndpoint.cs`.
- **Repositories** exist only for cached queries.
- **Mediator:** `Govern.Utils.Mediator`. Requests are `IRequest<TRequest, TResponse>`, handlers return `Result<TResponse>`, and the pipelines are error handling → logging → validation.
- **Errors:** expected failures are returned as `Error` (`[Feature]/Models/XxxErrors.cs`); only bugs throw.
- **Mapping:** no mapping libraries. Use EF `Select` projections; reused projections are static `Projection`/`From` members on the target type.
- **Packages:** never pick versions from memory. Use `dotnet add package`, which writes to central `Directory.Packages.props`.

## MCP servers

- `cwm-roslyn-navigator`: **read-only analysis.**
  - Use `find_symbol`, `find_references` and `find_callers` to navigate.
  - Use `get_project_graph`, `get_endpoint_map` and `get_di_registrations` to understand structure.
  - Use `detect_antipatterns`, `find_dead_code` and `analyze_change_impact` to check quality.
  - Use `resolve_stack_trace` for exceptions.
  - Prefer these over reading whole files.
- `roslyn-refactor`: **code changes.** Rename, move type or namespace, change signature, extract method or interface, add missing usings, format. Use it for multi-file refactors instead of manual edits.
- `microsoft-docs` / `context7`: current .NET, ASP.NET Core, EF Core, Radzen and Testcontainers APIs. Check them before using an API you aren't sure of.
- `postgres`: inspect the local database (it is unrestricted; don't run destructive SQL without asking).

## Claude setup

| Kind | Items |
|---|---|
| Rules, always loaded | `project-map`, `planning` |
| Rules, path-scoped | `architecture` (src), `csharp-style` (*.cs), `xml-docs`, `logging`, `mapping`, `error-handling` (src/*.cs), `persistence` (Domain, Application, Infrastructure), `api-endpoints` (Api), `auth` (Api, Users, Identity), `security` (src, infra, .github, appsettings), `unit-tests`, `integration-tests`, `architecture-tests`, `docs` |
| Skills | `/develop-feature` (the dev flow), `/verify` (quality gate), `/add-use-case`, `/add-entity`, `/add-migration`, `/add-infra-concern` (recipes from the real Users slice), `/outdated-packages` (manual only) |
| Agents (read-only, project memory) | `code-reviewer` (after every feature), `security-auditor` (before every stage → main release PR, and after auth, endpoint, config, infra or CI changes), `ef-core-specialist` (entities, configurations, migrations, slow queries) |
| Hooks (`.claude/hooks/`) | bash guard (destructive git, protected branches, `rm -rf`), protected generated/secret files, whitespace format on edit, restore on csproj/props edit, test gate on Stop, branch info on SessionStart |

Current state and remaining roadmap (Blazor client next): `docs/plans/template-solution-roadmap.md`.
