---
title: Users feature (sign-up, sign-in, refresh, current user)
tags: [users, auth, jwt, refresh-token, sign-up, sign-in, email]
code: [src/Govern.Application/Users, src/Govern.Api/Features/Users, src/Govern.Domain/Entities/User.cs, src/Govern.Domain/Entities/RefreshToken.cs, src/Govern.Infrastructure/Identity]
updated: 2026-09-27
---

# Users feature

The Users feature lets people create an account with email and password, sign in to receive an access token and a refresh token, keep the session alive by rotating refresh tokens, and read their own account.

## Endpoints

| Method | Route | Access | Success | Errors |
|---|---|---|---|---|
| POST | `/api/users/sign-up` | Anonymous, rate limited | 201 `{ userId }`, `Location: /api/users/me` | 400 validation, 409 `user.email_taken`, 429 |
| POST | `/api/users/sign-in` | Anonymous, rate limited | 200 `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt }` | 400, 401 `user.invalid_credentials`, 429 |
| POST | `/api/users/refresh` | Anonymous, rate limited | 200 same shape as sign-in | 400, 401 `user.invalid_refresh_token`, 429 |
| GET | `/api/users/me` | Bearer token | 200 `{ id, email, createdAt }` | 401, 404 `user.not_found` |

Errors are RFC 9457 ProblemDetails with a stable `code` extension. Validation errors list the invalid fields.

## Signing up

- The email must be a valid address (max 320 characters). It is normalised (trimmed, lower-case), and it must be unique regardless of case.
- The password must be 8–128 characters. Only a PBKDF2 hash is stored (ASP.NET Core Identity `PasswordHasher`).
- A successful sign-up raises `UserSignedUpEvent`. After the save, `UserSignedUpEventHandler` sends a "Welcome to Govern" email through `IEmailService`. Email delivery is currently mocked (`LoggingEmailService` only logs the subject).

## Signing in

- A correct email and password return a 15-minute access token (JWT) and a 14-day refresh token.
- Unknown email and wrong password return the same 401 `user.invalid_credentials`, with comparable response time. The Api never reveals whether an account exists.
- Each sign-in creates a new refresh token, so one user can be signed in on several devices.

## Refreshing tokens

- A refresh token is single-use. Exchanging it returns a new pair and revokes the presented token (`revoked_at`, `replaced_by_token_id`).
- Presenting a token that was already revoked is treated as theft: every active refresh token of that user is revoked and a Warning is logged with the user id. The user must sign in again on all devices.
- Unknown, expired and revoked tokens all return 401 `user.invalid_refresh_token`.
- Only the SHA-256 hash of a refresh token is stored in the database.
- **Known limitation:** two simultaneous refreshes with the same token can both succeed. Optimistic concurrency on `refresh_tokens` would prevent this.

## Current user ("who am I")

`GET /api/users/me` returns the account of the user in the access token's `sub` claim. If the account was deleted after the token was issued, it returns 404 `user.not_found`.

## Data model

| Table | Columns | Notes |
|---|---|---|
| `users` | `id` (Guid v7), `email` (unique), `password_hash`, `created_at`, `updated_at` | Aggregate root |
| `refresh_tokens` | `id`, `user_id` (FK, cascade), `token_hash` (unique), `expires_at`, `revoked_at`, `replaced_by_token_id`, `created_at`, `updated_at` | Created and revoked only through `User` |

## Configuration

`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (secret), `Jwt:AccessTokenLifetime`, `Jwt:RefreshTokenLifetime`, `RateLimiting:AuthPermitLimit`, `RateLimiting:AuthWindow`. See [configuration](../operations/configuration.md).

## Related

- [ADR 0002: auth tokens](../decisions/0002-auth-tokens.md)
- Rules: `.claude/rules/auth.md`, `security.md`
- Tests: `tests/Govern.IntegrationTests/Features/Users/`
