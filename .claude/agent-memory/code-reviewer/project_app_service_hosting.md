---
name: app-service-hosting-checks
description: Govern App Service/release facts for infra reviews - Health check host, host allow-list vs API_ORIGIN derivation, release step ordering
metadata:
  type: project
---

Review 6 (2026-09-27, feature/health-host-filtering, follow-ups #8/#9). Verified facts:
- MS Learn (monitor-instances-health-check FAQ): the App Service Health check pings the path on the **default domain**, over HTTPS when HTTPS Only is on, needs 200-299, doesn't follow redirects. So "internal probes use an IP Host" is not what the docs say; the `/health/live` host exemption is defence in depth only.
- App Service front ends route by Host, so a spoofed Host never reaches the container via the public endpoint; `HostAllowListMiddleware` is defence in depth.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` processes only X-Forwarded-For/Proto (not Host), with KnownNetworks cleared.
- Release order: Bicep deploy (sets app settings, new image) runs **before** `configure-client.sh` validates API_ORIGIN. Anything derived from API_ORIGIN in `main.bicepparam` (`apiCustomHostnames`) is unvalidated when it reaches the app; a value the Api's `ValidateOnStart` rejects (port, `HTTPS://`, `http://`) crash-loops prod. `az bicep build-params` with env vars reproduces derivations quickly.

**How to apply:** for new infra settings derived from repo variables, check they are validated before `az deployment` and agree with the app's options validator. Related: [[convention-sweep-gaps]].

Status (same day, re-review): fixed. release.yml validates API_ORIGIN (`^https://[a-z0-9-]+(\.[a-z0-9-]+)+$`) before deploy, and configure-client.sh uses the same regex. The allow-list must be non-empty outside Development (containers run as Production, so local `docker run` needs `HostFiltering__AllowedHosts__0`). The tag lives in `Application.Common/Models/HealthCheckTags`. Don't re-report these.
