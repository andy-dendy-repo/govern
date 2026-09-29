---
title: "ADR 0002: JWT access tokens with rotating refresh tokens"
tags: [adr, auth, jwt, refresh-token, security, blazor, web-locks]
code: [src/Govern.Infrastructure/Identity, src/Govern.Application/Users, src/Govern.Api/DependencyInjection.cs, src/Govern.Client/Auth]
updated: 2026-09-28
---

# ADR 0002: JWT access tokens with rotating refresh tokens

## Status
Accepted (2026-09-27).

## Context
The Api is consumed by a Blazor WebAssembly client hosted on a different origin (Azure Static Web Apps). Authentication must work with a bearer header, stay cheap to validate, and let sessions last longer than an access token without re-entering passwords.

## Decision
- **Access tokens:** HS256 JWTs.
  - Claims: `sub`, `email`, `jti`, issuer and audience.
  - Lifetime: 15 minutes.
  - Validation: issuer, audience, lifetime (30 s clock skew) and signature, with only HS256 accepted.
  - The signing key is a secret of at least 256 bits, held in Key Vault.
- **Refresh tokens:**
  - Opaque, 64 random bytes, lifetime 14 days.
  - Stored only as SHA-256 hashes.
  - Single-use: every refresh revokes the presented token and issues a new pair.
- **Reuse detection:** a rotated refresh token presented again, to refresh or to sign out, revokes all of the user's active refresh tokens and logs a warning. Tokens revoked by sign-out or the session cap are only rejected.
- **Sessions:** a soft cap of `Jwt:MaxActiveRefreshTokens` active refresh tokens per user (default 10); signing in beyond the cap revokes the least recently used one. Users can sign out on one device or everywhere. Expired tokens are deleted on sign-in and refresh, with no background job.
- **Passwords:** hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2), without adopting the rest of ASP.NET Core Identity.
- **Deny by default:** a fallback authorization policy requires an authenticated user, and every endpoint declares its access explicitly.
- **Client:** keeps both tokens in `localStorage`, refreshes ahead of the access token's expiry and once after a 401, and serialises refreshes across tabs with a Web Lock. Implemented in `src/Govern.Client/Auth`; see [client authentication](../features/client.md#client-authentication-sign-in-and-token-refresh).

## Alternatives considered
- **Cookies with the backend-for-frontend pattern:** safer against XSS, but they need cookies on a shared site or a BFF host. That doesn't fit a static client plus a separate Api host on the cheapest tiers.
- **Tokens in memory only:** safest, but every page reload signs the user out.
- **ASP.NET Core Identity endpoints:** too much surface (and schema) for the minimal slice.

## Consequences
- `localStorage` is readable by injected script. XSS prevention (a strict CSP on the Static Web App, no raw HTML rendering) becomes a security requirement for the client.
- The short access-token lifetime limits the damage from a leaked token, and rotation plus reuse detection limits the damage from a leaked refresh token.
- Revoking access immediately (e.g. a disabled account, or sign-out) takes up to 15 minutes unless a token denylist is added: sign-out revokes refresh tokens only.
- Tabs of the client share one refresh token. Because presenting a rotated token again counts as reuse, two tabs refreshing at once would sign the user out everywhere, so the client refreshes, and signs out, under a cross-tab Web Lock (`navigator.locks`) and re-reads the stored tokens inside it. Sign-out refreshes first when the access token is due, so it always presents the current refresh token. A browser without Web Locks falls back to an unlocked refresh; when the lock module fails to load, the refresh is deferred instead.
- A revoked token that has expired and been deleted can no longer be recognised as reused. Detection covers the whole refresh token lifetime, which is the window in which a stolen token is usable.
- Symmetric signing means only the Api can issue and validate tokens. Moving to asymmetric keys is needed if other services must validate them.
