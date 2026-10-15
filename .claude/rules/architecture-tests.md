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
| `Conventions/Templates.cs` | UF / AGFT / 3DFT / CFT (client) / Core folder specs per project (single source of truth) | data (`FolderSpec`) |
| `ProjectReferenceTests` | csproj `ProjectReference`s match `Layers`; every `src/` project is registered; `Govern.UnitTests` references only `Layers.UnitTestReferences` (Core, Domain, Utils); `Govern.ClientTests` references only Client; `Govern.Core` has no `PackageReference` | XML scan of `.csproj` |
| `FolderStructureTests` | Allowed folders/root files (`.cs` and `.razor`); `Contracts/*Contract.cs` contain only prefixed Request/Response/Validator/Handler (+ `file` types); Core `Actions/*Action.cs` contain exactly `XxxCommand` + `XxxRule` (+ `file` types); one endpoint per `Features` file | filesystem + source scan (`Support/TypeDeclarations`) |
| `LayerDependencyTests` | Type-level dependencies between layers; Core free of any `Microsoft.*` type; Domain free of EF Core / ASP.NET Core; Application free of ASP.NET Core; `Api.Features` free of Infrastructure / Domain; no mapping libraries; Client uses `System.Net.Http` and `System.Net.ServerSentEvents` only in `Features/*/Clients`, `Auth`, `Extensions/HttpClientExtensions` and the root `DependencyInjection`, `Microsoft.JSInterop` only in `Auth` and (feature) `Services`; Client uses Core for views and previews only (no `Unit`, `City`, `GameState`, `GameFactory`, `GameSetup`, `CommandResult`, `GameUpdate`, `DeterministicRandom`, `GameSerializer`) | ArchUnitNET |
| `CodeMetricsTests` | Size limits from `Conventions/CodeLimits.cs`: file lines, member body lines, method and constructor parameters (generated code excluded); `.razor` file lines and `@code`/`@functions` block lines | Roslyn syntax parse of `src/**/*.cs`; line scan of `src/**/*.razor` |
| `NamingConventionTests` | No `Helper` suffix; suffix per folder; contract types; sealed `IRequestHandler` handlers; static `*Endpoint` classes with `Map(IEndpointRouteBuilder)`; endpoint parameters carrying Core game types (Core enums excepted) and every `Api/Models/*Body` declare `static ValueTask<T?> BindAsync(HttpContext)` (reflection); entities derive from `BaseEntity`, events from `BaseEvent`; value objects are sealed, immutable and not entities; sealed `INotificationHandler` event handlers; `I` prefix; Abstractions abstract; Extensions static; Enums only enums; Client pages are `XxxPage` components, Layout/Components hold components, Clients are sealed `XxxClient` | ArchUnitNET |
| `ClientSecurityTests` | `staticwebapp.config.json` CSP directives and security headers; no script directive allows inline, eval, wildcards or scheme sources; routes never override headers and block `/.auth/*`; `index.html` without external assets, inline scripts (except the empty import map) or `on*=` handlers; no raw HTML (`MarkupString`, `AddMarkupContent`, Radzen HTML/Markdown) or eval in Client `.cs`, `.razor` and `wwwroot/**/*.js`; no HTML or code sinks (`innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `new Function`, string `setTimeout`/`setInterval`) in `wwwroot/**/*.js`; `localStorage` only in `Auth/TokenStore.cs`; no secret-like keys in `wwwroot/appsettings*.json` | JSON/HTML/source scan |
| `GameUiTests` | Game UI design system (`game-ui.md`): no colour values (hex, functional, common names) in `wwwroot/css/*.css` except `tokens.css` or in Razor `fill`/`stroke`/`color`/`background`/`style` attributes; every terrain id in `rulesets/**/*.json` has a `--gv-terrain-<id>` token and a `.terrain-<id>` icon rule in `board.css`, and every river kind a `.river-<id>` swatch rule; the board's scripts (`js/board3d`) read only tokens that exist, `--gv-board3d-*` ones have dark values, and every model kind has its builder | CSS/Razor/JSON scan |
| `RulesReferenceTests` | The player rules reference `docs/game/rules.md` matches the default ruleset (loaded like the Api, via `DefaultRulesetService`): the "Terrain types" and "Unit types" tables have exactly one row per ruleset terrain and unit (by its `texts` name) with its numbers, class and ability labels (every `UnitAbility` needs its label in the test), the "Class counters" table has one row per `combat.counters` entry, and the "Game numbers" table matches the setup, map, city and combat sections | Markdown table scan |
| `ClientContractTests` | A Client DTO in `Features/*/Models` named like a public `Govern.Application` type has only properties the server type has, with the same shape (framework types identical after unwrapping nullables and collections, Govern types and enums by name, enum members a subset); the Users (`SignInRequest`) and Games (`GetGameResponse`, `SendGameCommandRequest`) pairs must be found; `ExplicitPairs` maps differently named Auth types (`TokenPair` ↔ `SignInResponse`/`RefreshTokenResponse`, the refresh body ↔ `RefreshTokenRequest`) and requires the same property set | reflection over both assemblies |

`Support/GovernArchitecture` loads the Govern assemblies once (all layered projects, including Core and Client), plus `Microsoft.AspNetCore.Components` so assignability rules can follow `LayoutComponentBase : ComponentBase`. `Support/SourceTree` finds the repo root (`Govern.slnx`) and skips `bin/`, `obj/`, `Properties/`, `wwwroot/` and dot-folders.

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
