---
paths:
  - "src/Govern.Api/**"
  - "src/Govern.Application/Users/**"
  - "src/Govern.Application.Common/**"
  - "src/Govern.Infrastructure/Identity/**"
  - "tests/Govern.IntegrationTests/**"
---

# Authentication and authorization

## Tokens

| Token | Format | Lifetime | Storage |
|---|---|---|---|
| Access | JWT, HS256, claims `sub` (user id), `email`, `jti`; `iss` / `aud` from `Jwt` options | 15 min (`Jwt:AccessTokenLifetime`) | Client memory or storage; never logged |
| Refresh | 64 random bytes, base64url, opaque | 14 days (`Jwt:RefreshTokenLifetime`) | DB stores only the SHA-256 hash (`refresh_tokens.token_hash`) |

- **Issuing:** `ITokenService` (`Infrastructure/Identity/Services/JwtTokenService`) issues tokens; `UserTokenIssuingService` (Application) combines access and refresh issuing for sign-in and refresh.
- **Validation (`Api/DependencyInjection`):**
  - issuer, audience, lifetime and signature are validated
  - only HS256 is accepted
  - `ClockSkew` is 30 s
  - `MapInboundClaims = false`, so `sub` stays `sub`
- **Signing key:**
  - at least 32 characters, never in appsettings
  - locally: user-secrets
  - Azure: Key Vault secret `Jwt--SigningKey`
- **Refresh rotation:**
  - Every refresh revokes the presented token and records `ReplacedByTokenId`.
  - Presenting an already revoked token counts as theft: `User.RevokeAllRefreshTokens` runs and a Warning is logged with the user id.
  - Expired and unknown tokens return the same `user.invalid_refresh_token` (401).
- **Passwords:**
  - `IPasswordHashingService` wraps Identity's `PasswordHasher` (PBKDF2)
  - length 8–128
  - never logged or returned
- **Sign-in:** unknown email and wrong password return the same error, and a hash is always verified, so the timing doesn't reveal which accounts exist.

## Authorization

- **Deny by default:** a fallback policy requires an authenticated user. Every endpoint still declares access explicitly: `.RequireAuthorization()` / `.RequireAuthorization(policy)` or `.AllowAnonymous()`.
- **Current user:** use `ICurrentUserService.UserId` (it reads `sub`). Handlers never read `HttpContext`.
- **Ownership:** handlers filter by the current user id (`Where(x => x.OwnerId == userId)`) and return NotFound for other users' resources rather than Forbidden, so the Api doesn't reveal that the resource exists.
- **Roles and policies** are added when needed. Policy names become constants in `Api/Models/` (the pattern follows `RateLimitingPolicies`), and every policy gets integration tests for 401 and 403.

## Client (future Blazor WASM)

The client will store the access and refresh tokens in `localStorage`, send `Authorization: Bearer`, refresh on 401 once, and sign out when the refresh fails. The trade-off is recorded in `docs/decisions/0002-auth-tokens.md`.

## Testing

- `UsersApi.CreateSignedInUserAsync` gives a real signed-in user through the public endpoints. Never mint tokens by hand in tests.
- Time-dependent behaviour (expiry) uses a derived factory with `FakeTimeProvider` (see `RefreshTokenEndpointTests.Post_ExpiredToken_Returns401`).
