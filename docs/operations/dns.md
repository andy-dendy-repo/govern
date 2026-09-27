---
title: DNS with Cloudflare and Azure custom domains
tags: [dns, cloudflare, custom-domain, tls, app-service, static-web-apps]
code: [infra/main.bicep]
updated: 2026-09-27
---

# DNS with Cloudflare and Azure custom domains

DNS is hosted on Cloudflare. Azure serves the Api (App Service) and the client (Static Web App). Custom domains are bound manually once, because Azure must verify DNS ownership before the binding can exist.

## Records

| Host | Type | Target | Cloudflare proxy |
|---|---|---|---|
| `api.<domain>` | CNAME | Bicep output `apiHostname` (`<app>.azurewebsites.net`) | DNS only (grey) while binding; see TLS below |
| `asuid.api.<domain>` | TXT | Web App "Custom domain verification ID" | — |
| `app.<domain>` (or apex) | CNAME | Bicep output `clientHostname` (`<name>.azurestaticapps.net`) | DNS only (grey) |

## Binding steps

1. Create the records with the proxy **off**.
2. **Api:**
   - Portal → Web App → Custom domains → Add, choosing "App Service managed certificate". Or use the CLI:
     ```bash
     az webapp config hostname add --webapp-name <app> -g <rg> --hostname api.<domain>
     ```
   - Then create a managed certificate and a TLS binding.
3. **Client:** Portal → Static Web App → Custom domains → Add, using CNAME validation. Azure issues the certificate.
4. **CORS:** add `https://app.<domain>` to `extraCorsOrigins` in `infra/main.bicepparam` and release.

## TLS and the Cloudflare proxy

- **Simplest:** keep the records DNS-only and let Azure-managed certificates serve TLS.
- **To use the Cloudflare proxy (orange):**
  - Set the SSL/TLS mode to **Full (strict)**; Azure still needs a valid certificate for the hostname.
  - Note that App Service managed certificates don't renew behind the proxy. Use a Cloudflare Origin Certificate uploaded to App Service instead.
  - With the proxy on, the client IP arrives in `CF-Connecting-IP`. Rate limiting currently keys on the forwarded client IP from App Service, so review this before enabling the proxy.
