---
name: openapi-checks
description: Verified Microsoft.AspNetCore.OpenApi 10 / Microsoft.OpenApi 2.x facts for Govern OpenAPI + Swagger UI reviews (transformer order, security requirement serialisation)
metadata:
  type: project
---

Review 7 (2026-09-27, feature/swagger-ui). Verified from source, don't re-derive:
- `OpenApiDocumentService.GetOpenApiDocumentAsync` (release/10.0): operation transformers run inside `GetOpenApiPathsAsync` **before** document transformers; afterwards `document.Workspace ??= new(); Workspace.RegisterComponents(document)`. So an `OpenApiSecuritySchemeReference(id, context.Document)` created in an operation transformer resolves lazily at serialisation, even though the scheme is added later by a document transformer.
- Microsoft.OpenApi 2.7.5 `OpenApiSecurityRequirement` serialisation **silently drops** keys whose reference is unresolved (`CanSerializeSecurityScheme`: Target null and id not in host doc's SecuritySchemes). A wrong id or a null host document produces no error, just a missing `security` entry, so a test must assert the `security` array on the serialized document.
- Govern pins Microsoft.OpenApi 2.7.5 centrally (AspNetCore.OpenApi 10.0.8 asks for 2.0.0).
- Swagger UI (Swashbuckle.AspNetCore.SwaggerUI) is middleware, so it must sit before `UseAuthorization` (fallback policy); a 401 in a "Production = not served" test doesn't prove "not served" (an authenticated 404 does).

**How to apply:** for OpenAPI transformer changes, check ids match `JwtBearerDefaults.AuthenticationScheme` and that the anonymous set mirrors `EndpointAuthorizationTests.IntentionallyAnonymous`. Related: [[convention-sweep-gaps]] (security-auditor.md "OpenAPI is Development-only" line).
