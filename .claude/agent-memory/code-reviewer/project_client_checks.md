---
name: client-checks
description: Govern Blazor client review checks - arch-rule namespace exemptions, configure-client.sh behaviour, NotFoundPage layout, facts already verified
metadata:
  type: project
---

Review 5 (2026-09-27, feature/blazor-client-foundation, follow-up #10a). Facts I verified, so don't re-derive them:
- .NET 10 standalone WASM: the environment is baked in at build/publish (`WasmApplicationEnvironmentName`; Production for publish). The `Blazor-Environment` header is ignored. The boot config lists appsettings files without integrity hashes, so post-publish edits of `appsettings.Production.json` are safe.
- `Router.NotFoundPage` renders through the app's `Found` fragment (`RouteView` with DefaultLayout), so a page without `@layout` still gets `MainLayout`.
- `configure-client.sh`: the Python hash equals the browser's (universal newlines match HTML CR/LF normalisation). Re-runs are idempotent. A trailing slash or empty host fails. Publish emits `.br/.gz` for the config and appsettings, and the script deletes them.
- Arch rule gaps I reported: the HttpClient rule regex `^Govern\.Client\.(?!…)` exempts the root namespace (needed for `AddHttpClient` in DependencyInjection, but undocumented). The JSInterop rule exempts only the root `Services`, not `Features/*/Services`.

**Why:** Plan 7 (auth pages, #10b) lands in these folders next and relies on these rules.
**How to apply:** when reviewing Plan 7, check that typed clients don't leak `HttpRequestException` to pages (pages can't depend on System.Net.Http), that refresh in `AuthorizationMessageHandler` doesn't resolve `UsersClient` (DI cycle), and that `localStorage` is touched only in `Auth/TokenStore`. Related: [[convention-sweep-gaps]].
