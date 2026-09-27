---
title: Users feature (sign-up, sign-in, refresh, sign-out, current user)
tags: [users, auth, jwt, refresh-token, sign-up, sign-in, sign-out, session-cap, email]
code: [src/Govern.Application/Users, src/Govern.Api/Features/Users, src/Govern.Domain/Entities/User.cs, src/Govern.Domain/Entities/RefreshToken.cs, src/Govern.Infrastructure/Identity]
updated: 2026-09-27
---

# Users feature

The Users feature lets people create an account with email and password, sign in to receive an access token and a refresh token, keep the session alive by rotating refresh tokens, sign out on one device or everywhere, and read their own account.

## Endpoints

| Method | Route | Access | Success | Errors |
|---|---|---|---|---|
| POST | `/api/users/sign-up` | Anonymous, rate limited | 201 `{ userId }`, `Location: /api/users/me` | 400 validation, 409 `user.email_taken`, 429 |
| POST | `/api/users/sign-in` | Anonymous, rate limited | 200 `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt }` | 400, 401 `user.invalid_credentials`, 429 |
| POST | `/api/users/refresh` | Anonymous, rate limited | 200 same shape as sign-in | 400, 401 `user.invalid_refresh_token`, 429 |
| POST | `/api/users/sign-out` | Bearer token | 204 | 400, 401 |
| POST | `/api/users/sign-out-everywhere` | Bearer token | 204 | 401 |
| GET | `/api/users/me` | Bearer token | 200 `{ id, email, createdAt }` | 401, 404 `user.not_found` |

Errors are RFC 9457 ProblemDetails with a stable `code` extension. Validation errors list the invalid fields.

## Signing up

- The email must be a valid address: exactly one `@` that is neither first nor last, and at most 320 characters both as entered (request validators) and after normalisation (`Email`). It must not contain whitespace, control characters (CR/LF, NUL), format or other invisible characters (zero-width space and joiners, combining grapheme joiner, Hangul fillers, variation selectors), private-use or unassigned code points, or broken UTF-16. This blocks header injection once real email delivery exists, and invisible lookalike addresses. Visible homoglyphs (Cyrillic `а`, fullwidth letters) are **not** prevented. Non-ASCII letters are allowed. The email is normalised (trimmed, lower-case, Unicode NFC; decomposed before lower-casing so the result is stable), so it is unique regardless of case or Unicode composition (`e` + combining accent equals `é`). These rules live in the `Email` value object (`src/Govern.Domain/ValueObjects/Email.cs`), which `SignUpValidator` and both handlers use, so a lookup can never skip normalisation. Concurrent sign-ups with the same email create exactly one account; the others get 409.
- Stored emails are re-validated when loaded (the EF converter calls `Email.Create`). Tightening the `Email` rules therefore needs a check that no stored row breaks them, or a row migration, before release.
- **Accepted risk:** 409 `user.email_taken` reveals that an email is registered, and the sign-up rate limit only slows enumeration down. Removing the risk needs email verification: always return 202 and email the owner.
- The password must be 8–128 characters. Only a PBKDF2 hash is stored (ASP.NET Core Identity `PasswordHasher`).
- A successful sign-up raises `UserSignedUpEvent`. After the save, `UserSignedUpEventHandler` sends a "Welcome to Govern" email through `IEmailService`. Email delivery is currently mocked (`LoggingEmailService` only logs the subject). Event delivery is best-effort: if the email fails, the sign-up still succeeds and the failure is logged.

## Signing in

- A correct email and password return a 15-minute access token (JWT) and a 14-day refresh token.
- A malformed email, an unknown email and a wrong password all return the same 401 `user.invalid_credentials`, with comparable response time, so sign-in does not reveal whether an account exists (sign-up does; see above).
- Each sign-in creates a new refresh token, so one user can be signed in on several devices.

## Session cap and refresh token cleanup

- **Session cap:** a user holds up to `Jwt:MaxActiveRefreshTokens` active refresh tokens (default 10), one per signed-in device. A sign-in beyond the cap revokes the least recently used session first: the active token with the earliest expiry (`User.IssueRefreshToken`). That device gets 401 `user.invalid_refresh_token` on its next refresh; the other sessions are not affected.
- The cap is **soft**: concurrent sign-ins below the cap can each add a token, so the count can briefly exceed it by the number of simultaneous sign-ins. The next sign-in evicts back down to the cap.
- Evicting a session races with a refresh or sign-out of that same token (`revoked_at` is a concurrency token). `SignInHandler` then reloads and retries (up to 3 attempts); the password is verified only once.
- **Cleanup without a background job:** every sign-in and every successful refresh deletes that user's expired refresh tokens in one `ExecuteDeleteAsync` (`RefreshTokenCleanupService`). Revoked but unexpired tokens are kept, because reuse detection needs them until they expire. Rows per user are therefore bounded by the refresh token lifetime: at most the rotations each active session made in the last 14 days (`Jwt:RefreshTokenLifetime`), limited by the per-IP `auth` rate limit on `/api/users/refresh`. Users who never return keep their last rows until the account is deleted (cascade).

## Refreshing tokens

- A refresh token is single-use. Exchanging it returns a new pair and revokes the presented token (`revoked_at`, `replaced_by_token_id`).
- **Reuse detection:** presenting a token that was already *rotated* (it has a `replaced_by_token_id`) to refresh or to sign out is treated as theft: every active refresh token of that user is revoked and a Warning is logged with the user id (EventId 1001, `RefreshTokenReuseService`). The user must sign in again on all devices.
- Tokens revoked by sign-out, by the session cap or by an earlier theft response have no successor. Presenting them again just returns 401, without revoking the user's other sessions.
- Unknown, expired and revoked tokens all return 401 `user.invalid_refresh_token`.
- Only the SHA-256 hash of a refresh token is stored in the database.
- Concurrent refreshes with the same token: exactly one succeeds, the rest get 401 (`revoked_at` is an optimistic concurrency token).

## Signing out

- `POST /api/users/sign-out` with `{ refreshToken }` revokes that refresh token for the authenticated caller and returns 204. Unknown, already revoked and other users' tokens also return 204 and change nothing, so the response reveals nothing about tokens.
- Signing out with a token that was already **rotated** is reuse, exactly as on refresh: every active refresh token of the caller is revoked and EventId 1001 is logged (`RefreshTokenReuseService`), still with 204. Otherwise an attacker who rotated a stolen token first would survive the victim's sign-out. A refresh that rotates the token during the sign-out makes the save conflict; the handler reloads and applies the same rule (up to 3 attempts).
- `POST /api/users/sign-out-everywhere` revokes every active refresh token of the caller and returns 204. A refresh that rotates a token at the same moment makes the save fail on the `revoked_at` concurrency token; the handler reloads and retries (up to 3 attempts), so no session survives. If all attempts conflict (only possible with sustained concurrent rotation), the request fails with 500 and can be retried.
- Both endpoints need a valid access token. A client whose access token has expired refreshes first, then signs out.
- Access tokens are not revoked: they stay valid until they expire (at most 15 minutes, `Jwt:AccessTokenLifetime`). Revocation always completes, even if the client disconnects.

## Current user ("who am I")

`GET /api/users/me` returns the account of the user in the access token's `sub` claim. If the account was deleted after the token was issued, it returns 404 `user.not_found`.

## Data model

| Table | Columns | Notes |
|---|---|---|
| `users` | `id` (Guid v7), `email` (unique), `password_hash`, `created_at`, `updated_at` | Aggregate root |
| `refresh_tokens` | `id`, `user_id` (FK, cascade), `token_hash` (unique), `expires_at`, `revoked_at`, `replaced_by_token_id`, `created_at`, `updated_at` | Created and revoked only through `User` |

## Configuration

`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (secret), `Jwt:AccessTokenLifetime`, `Jwt:RefreshTokenLifetime`, `Jwt:MaxActiveRefreshTokens`, `RateLimiting:AuthPermitLimit`, `RateLimiting:AuthWindow`. See [configuration](../operations/configuration.md).

## Related

- [ADR 0002: auth tokens](../decisions/0002-auth-tokens.md)
- Rules: `.claude/rules/auth.md`, `security.md`
- Tests: `tests/Govern.IntegrationTests/Features/Users/`
