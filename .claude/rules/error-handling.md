---
paths:
  - "src/**/*.cs"
---

# Error handling

## Return expected failures, throw programming errors

| Situation | Do |
|---|---|
| Invalid input | Let the `XxxValidator` fail; the mediator's `ValidationPipeline` returns `Error.Validation` (400) before the handler runs |
| Business rule / state conflict (duplicate, not found, not allowed) | `return XxxErrors.Something;` from the handler (`Result<T>` converts implicitly) |
| Bug, broken invariant, infrastructure failure | Throw (or let it propagate). `ErrorHandlingPipeline` logs it once and returns `Error.Unexpected()` (500, no details) |
| Domain invariant violated by a caller (`User.SignUp("")`) | Throw `ArgumentException` / `InvalidOperationException` from the domain; it's a programming error |

- `Result<TValue>` and `Error` live in `Govern.Utils.Results`. Handlers return `ValueTask<Result<TResponse>>`.
- Feature errors are `static readonly Error` fields in `Application/[Feature]/Models/XxxErrors.cs`, e.g. `UserErrors.EmailTaken`. Never build ad-hoc errors inline in handlers.
- **Error codes** are stable, lower snake_case and namespaced by feature (`user.email_taken`). Clients may depend on them, so never rename one silently.
- **Messages** are safe for clients:
  - no ids of other users, no stack traces, no SQL
  - no hint whether an account exists (`user.invalid_credentials` covers both an unknown email and a wrong password)

## ErrorType → HTTP

`Api/Extensions/ResultExtensions.ToHttpResult()` maps errors to RFC 9457 ProblemDetails with a `code` extension:

| ErrorType | Status |
|---|---|
| Validation | 400 (`ValidationProblem` with field errors) |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Unexpected | 500 |

Exceptions outside the mediator are handled as follows:
- **Malformed JSON and binding errors:** `ExceptionHandlingMiddleware` returns 400.
- **Anything else:** logged and returned as a 500 ProblemDetails.
- **Empty error responses** (e.g. a 401 from authentication) get a ProblemDetails body from `UseStatusCodePages`.

## Aborted requests (client disconnects)

- A client disconnect cancels `HttpContext.RequestAborted`. Minimal APIs bind it to the endpoint's `CancellationToken`, which flows through the mediator into EF Core and cancels the running SQL. Always pass the token down so the work actually stops.
- **No response can be delivered.** The status code exists only for logs and metrics. `ExceptionHandlingMiddleware` records **499 Client Closed Request** (the nginx convention, `StatusCodes.Status499ClientClosedRequest`) with no body, and logs it at Debug (EventId 3002). It is never an Error or a 5xx; aborts are normal on mobile networks.
- `ErrorHandlingPipeline` rethrows `OperationCanceledException` when the request's token is cancelled, so aborts never become `Error.Unexpected`.
- **Don't confuse aborts with server-side timeouts.** A server giving up on slow work returns 503/504 (e.g. the RequestTimeouts middleware), and a client too slow to *send* its request gets 408 (Kestrel).
- **Commits are not undone by an abort.** If a write committed before the client went away, the client never sees the result, and a retry may hit a domain error (e.g. 409 on sign-up). Work that must finish once started (revocation, post-commit events) uses `CancellationToken.None`.
- Covered by `tests/Govern.IntegrationTests/Pipeline/AbortedRequestTests`.

## Rules

- Don't `try/catch` in handlers just to log. The pipeline logs unexpected exceptions once. Catch only to translate a *specific, expected* exception into an `Error`: `DbUpdateConcurrencyException`, or `DbUpdateException` filtered with `IDbExceptionClassificationService.IsUniqueViolation`.
- Never return `null` for "not found"; return a NotFound error.
- Never expose `exception.Message` to clients.
- Endpoints never inspect `Error.Type` themselves; they call `result.ToHttpResult(...)`.
