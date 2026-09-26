---
paths:
  - "src/**/*.cs"
---

# XML documentation

`GenerateDocumentationFile` is on for every `src/` project except Core (`Directory.Build.props`). With `TreatWarningsAsErrors`, a missing doc comment on a public or protected member (CS1591) **fails the build**.

## What to document

- Every public and protected type and member, including records and their positional parameters (`<param>` on the record).
- Internal types don't need docs. Add a `<summary>` anyway when the intent isn't obvious (handlers, interceptors).
- Implementations of a documented interface or override: use `/// <inheritdoc />`. Only write a new summary when the implementation adds behaviour worth knowing (caching, side effects).

## How

- `<summary>`: one or two sentences on **what it is for / what it guarantees**, not a restatement of the name. `GetUserById` → "Returns the user or a not-found error; soft-deleted users are excluded."
- `<param>`: the constraints, units and meaning of null.
- `<returns>`: what is returned and under which conditions.
- `<exception cref="…">`: every exception deliberately thrown.
- `<typeparam>`: generic parameters.
- Use `<see cref="…"/>` to link types and members, `<see langword="null"/>` / `true` / `false` for keywords, and `<c>` for inline code.
- Use `<remarks>` for design decisions and non-obvious behaviour; avoid long prose in `<summary>`.
- Keep the docs in sync when behaviour changes. Stale docs are worse than none.

## API endpoints

Endpoints are named methods, not lambdas, because the compiler does not capture XML comments on lambdas and OpenAPI reads the docs from the method. Document:
- the endpoint summary,
- each parameter,
- `<response code="…">` for every status code the endpoint returns.

## Not documented

Tests, `Program.cs` top-level statements, EF migrations (they are generated with `<inheritdoc />`), and Razor components.
