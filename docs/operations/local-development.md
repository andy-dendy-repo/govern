---
title: Local development
tags: [local, setup, postgres, docker, user-secrets, run, tests]
code: [src/Govern.Api, src/Govern.Api/Govern.Api.http, src/Govern.Client, global.json, dotnet-tools.json]
updated: 2026-09-27
---

# Local development

## Prerequisites

- **.NET SDK:** the version pinned in `global.json` (10.0.300, roll forward to the latest feature band).
- **Docker:** running. Integration tests start `postgres:17-alpine` through Testcontainers.
- **Local tools:** `dotnet tool restore` installs `dotnet-ef` and `cwm-roslyn-navigator`.

## Local database

```bash
docker run -d --name govern-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=govern -p 5432:5432 postgres:17-alpine
```

## Secrets (user-secrets, never appsettings)

```bash
cd src/Govern.Api
dotnet user-secrets set "ConnectionStrings:Govern" "Host=localhost;Port=5432;Database=govern;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

## Run the Api

```bash
dotnet run --project src/Govern.Api
```

- The Api listens on `http://localhost:5153` and `https://localhost:7294`.
- Migrations are applied at startup.
- The OpenAPI document is at `/openapi/v1.json` (Development only).
- `src/Govern.Api/Govern.Api.http` contains ready-made requests: sign-up → sign-in → me → refresh → health.
- The mocked welcome email appears in the console log.

## Run the client

```bash
dotnet run --project src/Govern.Client
```

- The client listens on `https://localhost:7042` and `http://localhost:5114`, and calls the Api at `Api:BaseUrl` (`https://localhost:7294`, `wwwroot/appsettings.json`).
- The Api's Development settings already allow both client origins in `Cors:AllowedOrigins`.
- The CSP from `staticwebapp.config.json` is not applied by the dev server; see [client](../features/client.md).

## Tests

```bash
dotnet test Govern.slnx                                  # everything (needs Docker)
dotnet test --project tests/Govern.IntegrationTests      # Api + Postgres
dotnet test --project tests/Govern.ClientTests           # Blazor components (bUnit)
dotnet test --project tests/Govern.ArchitectureTests     # conventions
dotnet test Govern.slnx --coverage --coverage-output-format cobertura
```

## Container image

```bash
dotnet publish src/Govern.Api -c Release -t:PublishContainer    # → govern-api:local
docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__Govern=Host=host.docker.internal;Database=govern;Username=postgres;Password=postgres" \
  -e "Jwt__SigningKey=<at least 32 chars>" govern-api:local
```

The image is Ubuntu Noble chiseled: non-root, no shell, port 8080.
