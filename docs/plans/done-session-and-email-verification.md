---
title: Session lifecycle, transactional sign-up email and email verification plans
tags: [plan, users, auth, refresh-token, sign-out, session-cap, email, email-verification, transaction]
code: [src/Govern.Domain/Entities/User.cs, src/Govern.Application/Users, src/Govern.Api/Features/Users]
updated: 2026-09-29
---

# Plans: session lifecycle, transactional sign-up email, email verification

**Status: done (closed 2026-09-29).** Plan 1 is delivered. Plans 2 and 3 moved to [next follow-ups](next-follow-ups.md) (items 4 and 5); their sections below stay as the detailed design for those items.

Increments, each on its own branch from `stage`, each run through `/develop-feature`, and each merged before the next starts. Plan 1 (refresh token pruning, session cap, sign-out) is done; see [users](../features/users.md). Together they replace backlog items #1 (sign-out), #4 (email verification), #6 (refresh token cleanup) and #7 (outbox) that used to be in [follow-ups](done-follow-ups.md). When a plan is merged, remove its section here; delete this file after Plan 3. There are no hosted jobs, no Azure Functions and no outbox.

---

# Plan 2: Send the sign-up email inside the database transaction

## Context
Today the welcome email is sent by `UserSignedUpEventHandler` after the commit, on a best-effort basis: if sending fails, the email is lost and only a log line remains. Instead of adding an outbox, the handler will flush the insert inside a transaction, send the email, and then commit. If sending fails, the sign-up rolls back and the client retries. The only remaining gap is "email sent, then commit failed", which is very rare because constraints are already checked by the flush.

## Scope
- In:
  - `SignUpHandler` transaction ordering
  - removing `UserSignedUpEventHandler`
  - replacing the outbox guidance in the persistence rule with this pattern (plus an ADR)
- Out:
  - a real email provider (`LoggingEmailService` stays)
  - email verification (Plan 3)

## Affected areas
- Application:
  - `Users/Contracts/SignUpContract.cs`
  - `Users/EventHandlers/UserSignedUpEventHandler.cs` (delete)
- Tests: `tests/Govern.IntegrationTests/Fixtures/FakeEmailService.cs`, `Features/Users/SignUpEndpointTests.cs`
- Code to reuse:
  - `IApplicationDbContext.BeginTransactionAsync`, and `IApplicationDbContextTransaction`, which rolls back on dispose
  - `IEmailService`, `EmailMessage`

## Steps
1. **Domain:** no change. `User.SignUp` still raises `UserSignedUpEvent`, now with no handler. Future handlers would run before the commit, inside the transaction.
2. **Application** (`SignUpHandler`):
   1. `await using var transaction = await db.BeginTransactionAsync(ct)`
   2. The existing `AnyAsync` pre-check, then `Add` and `SaveChangesAsync`, which flushes (unique-violation catch unchanged → `EmailTaken`).
   3. `await emailService.SendAsync(welcome, ct)`
   4. `await transaction.CommitAsync(CancellationToken.None)`: once the email is sent, a client abort must not undo the commit.

   If `SendAsync` throws, the exception propagates, the transaction is rolled back on dispose, and the pipeline returns 500. This follows the error-handling rule for infrastructure failures, so no new `ErrorType` is needed. Move `WelcomeSubject` onto `SignUpHandler`, and delete `UserSignedUpEventHandler`.
3. **Infrastructure / Api / Client:** no change.

## Tests
- Unit: none; this is not domain code.
- Integration (`SignUpEndpointTests`):
  - Existing: 201 plus one welcome email; duplicate email → 409 and no email.
  - New: when `FakeEmailService` is set to fail (add a `FailNext` switch), sign-up returns 500, no `users` row exists, and signing up again with the same email then succeeds.
- Architecture: none.

## Docs & rules
- `.claude/rules/persistence.md`, Interceptors section:
  - Replace the outbox bullet with: "A side effect that must succeed together with the save (e.g. an email) is not a domain event. In the handler: begin a transaction → `SaveChangesAsync` (flush) → the side effect → `CommitAsync(CancellationToken.None)`. Example: `SignUpHandler`."
  - Keep the best-effort note for ordinary events.
- `docs/decisions/0004-side-effects-in-transaction.md` (new ADR):
  - the decision: send inside the transaction rather than through an outbox or queue
  - the reasons: no hosted jobs, no extra infrastructure
  - the trade-offs: a connection is held during the provider call, sign-up depends on the email provider being up, and the rare "sent but not committed" case
- `docs/features/users.md`: the sign-up email paragraph.
- `docs/architecture.md` / `docs/README.md`: index the ADR.
- Remove this plan's section from `docs/plans/done-session-and-email-verification.md`.
- `.claude/agent-memory/code-reviewer/project_known_limitations.md`: update it if it mentions best-effort welcome email delivery.

## Verification
- `dotnet build Govern.slnx` and `dotnet test Govern.slnx` (`/verify`).
- Manual: sign up in `Govern.Api.http` → 201 and a "Welcome to Govern" log line from `LoggingEmailService`.

## Risks & open questions
- A slow email provider holds a database connection during sign-up (B1ms has about 50 connections). That's acceptable at current scale; the real provider (a later `/add-infra-concern`) must set a short HTTP timeout.

---

# Plan 3: Email verification

## Context
Sign-up returns 409 for registered emails, which lets anyone enumerate accounts, and anyone can register someone else's address. With verification, sign-up always returns 202 and the owner of the address is emailed. Sign-in requires a confirmed email, and a resend endpoint makes a lost email recoverable. That recoverability is why Plan 2 doesn't need an outbox.

## Scope
- In:
  - `EmailConfirmedAt` and a single-use confirmation token on `User` (stored hashed)
  - 202 sign-up
  - confirm and resend endpoints
  - sign-in blocked until the email is confirmed
  - existing accounts treated as confirmed
- Out:
  - a real email provider (a separate `/add-infra-concern`)
  - the Blazor confirmation page (Blazor client, #10)
  - password reset

## Affected areas
- Domain: `Entities/User.cs`
- Application.Common:
  - `Options/EmailConfirmationOptions.cs` (new)
  - `Abstractions/ITokenService.cs` (generalised)
- Application:
  - `Users/Contracts/SignUpContract.cs`, `SignInContract.cs`
  - `Users/Contracts/ConfirmEmailContract.cs` (new), `ResendConfirmationContract.cs` (new)
  - `Users/Models/UserErrors.cs`
  - `Users/Services/ConfirmationEmailService.cs` (new; builds and sends the confirmation email)
- Infrastructure:
  - `Persistence/Configurations/UserConfiguration.cs`
  - migration `AddEmailConfirmation`
  - `Identity/Services/JwtTokenService.cs` (rename only)
- Api:
  - `Features/Users/ConfirmEmailEndpoint.cs` (new), `ResendConfirmationEndpoint.cs` (new), `SignUpEndpoint.cs` (202)
  - `DependencyInjection.cs` (options binding)
- Infra: `infra/modules/appService.bicep` (`EmailConfirmation__LinkBaseUrl`)
- Code to reuse:
  - the random token and SHA-256 hash in `JwtTokenService`
  - Plan 2's transaction pattern
  - `IPasswordHashingService.SimulateVerify`, for equal timing
  - `RateLimitingPolicies.Auth`

## Steps
1. **Domain** (`User`):
   - `EmailConfirmedAt`, `EmailConfirmationTokenHash`, `EmailConfirmationTokenExpiresAt`.
   - `SignUp(email, passwordHash, confirmationTokenHash, expiresAt)`.
   - `IssueEmailConfirmationToken(hash, expiresAt)`, which replaces any previous token.
   - `ConfirmEmail(now)`: requires an unexpired token, sets `EmailConfirmedAt`, clears the token.
   - `IsEmailConfirmed`.
2. **Application.Common:**
   - Rename `ITokenService.CreateRefreshToken`/`HashRefreshToken` and `GeneratedRefreshToken` to `CreateOpaqueToken`/`HashOpaqueToken`/`GeneratedOpaqueToken` using `roslyn-refactor` rename.
   - `EmailConfirmationOptions` (section `EmailConfirmation`): `TokenLifetime` (default 1 day, range 10 min to 7 days) and `LinkBaseUrl` (`[Required]`, absolute URL), validated on start.
3. **Application:**
   - `ConfirmationEmailService.SendAsync(user, rawToken, ct)` sends a link built as `{LinkBaseUrl}#token={token}`. The fragment keeps the token out of server and proxy logs.
   - `SignUpHandler`:
     - Existing email: `SimulateVerify` (equal timing), send "You already have a Govern account", return 202.
     - New email: transaction → add the user with a token → flush → send the confirmation email → commit (Plan 2 pattern).
     - A concurrent unique violation also returns 202, with no email.
     - `SignUpResponse` becomes empty, and `UserErrors.EmailTaken` is removed.
   - `SignInHandler`: after a successful password check, if the email is unconfirmed, return `UserErrors.EmailNotConfirmed` (403 `user.email_not_confirmed`). This is safe to reveal because the password proved ownership.
   - `ConfirmEmailContract`: `ConfirmEmailRequest(string Token)`. Look the user up by token hash and call `ConfirmEmail(now)`. Unknown, expired or used tokens → `UserErrors.InvalidConfirmationToken` (404 `user.invalid_confirmation_token`).
   - `ResendConfirmationContract`: `ResendConfirmationRequest(string Email)`, always 202. For an existing, unconfirmed user: transaction → new token → flush → send → commit. Otherwise do nothing.
4. **Infrastructure:**
   - `UserConfiguration`: `email_confirmation_token_hash` max length 64, with a unique index filtered to `IS NOT NULL`.
   - Migration `AddEmailConfirmation`: three nullable columns, plus `migrationBuilder.Sql("UPDATE users SET email_confirmed_at = created_at")` so existing accounts keep working.
   - Review it with `ef-core-specialist`.
5. **Api:**
   - `POST /api/users/sign-up` → 202, with no body or `Location`.
   - `POST /api/users/confirm-email` (anonymous, rate limited) → 204, 400, 404, 429.
   - `POST /api/users/resend-confirmation` (anonymous, rate limited) → 202, 400, 429.
   - `POST /api/users/sign-in` adds 403.
   - Bind `EmailConfirmationOptions`, set `appsettings.json` defaults (`LinkBaseUrl` in appsettings.Development), and add the requests to `Govern.Api.http`.
6. **Infra:** Bicep app setting `EmailConfirmation__LinkBaseUrl`, pointing at the Static Web App's `/confirm-email`. `az bicep build`; run `security-auditor`, since this changes auth, endpoints and infra.
7. **Client:** none. The page arrives with #10.

## Tests
- **Unit** (`UserTests`):
  - `ConfirmEmail` sets the timestamp and clears the token
  - an expired token throws
  - a re-issued token replaces the old one
- **Integration:**
  - Sign-up:
    - 202 for both new and existing emails, with the right email captured by `FakeEmailService` (confirmation vs "already have an account")
    - concurrent same-email sign-ups create exactly one user, and all return 202
    - email failure → 500 and no user row (carried over from Plan 2)
  - Confirm:
    - happy path → 204, and sign-in then works
    - expired token (`FakeTimeProvider`) → 404
    - reused token → 404
    - empty token → 400
  - Resend:
    - unconfirmed user → 202, a new email is sent, and the old token → 404
    - unknown email → 202 with no email
    - confirmed user → 202 with no email
  - Sign-in:
    - unconfirmed → 403 `user.email_not_confirmed`
    - wrong password on an unconfirmed account → 401, which still reveals nothing
  - Update `UsersApi.CreateSignedInUserAsync` to confirm through the captured email token.
  - `EndpointAuthorizationTests`: add `/api/users/confirm-email` and `/api/users/resend-confirmation` to `IntentionallyAnonymous`.
- **Architecture:** none.

## Docs & rules
- `docs/features/users.md`:
  - the endpoints table
  - sections on confirming an email and resending confirmation
  - remove the enumeration "accepted risk"
  - the entity table (new columns)
- `docs/operations/configuration.md` and `docs/operations/deployment.md`: `EmailConfirmation:*`.
- Remove this plan's section from `docs/plans/done-session-and-email-verification.md`.
- `.claude/rules`: no convention change.

## Verification
- `dotnet build Govern.slnx`, `dotnet test Govern.slnx` (`/verify`), `az bicep build --file infra/main.bicep`.
- Manual in `Govern.Api.http`:
  1. Sign up → 202; the log shows the confirmation email subject.
  2. Sign in → 403.
  3. Take the token from the database or a debug log (never logged in production), confirm → 204.
  4. Sign in → 200.

## Risks & open questions
- **Timing on resend:** an unconfirmed account triggers an email send and an unknown email doesn't, so response time can hint that an account exists. It's rate limited, and the risk is noted in `users.md`. Sign-up avoids the same leak because both of its paths send an email.
- **Breaking change:** sign-up changes from 201 with `{ userId }` to 202. No client exists yet, so nothing breaks.
