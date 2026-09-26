---
paths:
  - "src/**/*.cs"
---

# Logging

## Mechanics

- Inject `ILogger<T>` (via the primary constructor). Never use a static logger or `Console.WriteLine`.
- **Use `[LoggerMessage]` source-generated methods**, not `logger.LogInformation(...)` calls. They avoid boxing and template parsing, and keep messages consistent. Declare them as a nested `private static partial class Log` in the class that logs (nested types are exempt from the folder suffix rules), or as `file static partial class` in the same file:

```csharp
public sealed partial class UserRegistrationService(ILogger<UserRegistrationService> logger)
{
    public async Task RegisterAsync(RegisterUserRequest request, CancellationToken ct)
    {
        // …
        Log.UserRegistered(logger, user.Id);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User {UserId} registered")]
        public static partial void UserRegistered(ILogger logger, Guid userId);
    }
}
```

- Message templates are structured, never interpolated or concatenated. Placeholders are PascalCase (`{UserId}`, `{ElapsedMs}`).
- Pass exceptions as the `Exception` parameter; don't put `ex.Message` into the template.
- **EventIds:** unique per class, grouped by area. Utils uses 1–999, Application 1000–1999, Infrastructure 2000–2999 and Api 3000–3999, with a range of 100 per feature or concern.

## Levels

| Level | Use for |
|---|---|
| `Trace` / `Debug` | Diagnostic detail, off in production |
| `Information` | Business events (user registered, order created), lifecycle (migrations applied) |
| `Warning` | Handled but unexpected: validation spikes, retries, degraded dependencies, auth failures |
| `Error` | Operation failed and the caller got an error; an exception is attached |
| `Critical` | Process-level failure (cannot connect to DB at startup) |

- Don't log and rethrow; log once at the boundary (middleware or background worker).
- Expected failures that are returned as results (validation, not found) are not errors. Log them at `Debug` or not at all.

## Never log

Passwords, password hashes, JWTs, refresh tokens, API keys, connection strings, full request or response bodies, or personal data beyond an internal id. Log `{UserId}`, not `{Email}`.

## Scopes and correlation

Middleware adds a logging scope with the trace or correlation id per request. Don't add request ids by hand in messages.
