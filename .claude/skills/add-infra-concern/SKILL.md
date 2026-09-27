---
name: add-infra-concern
description: >
  Adds an external integration to Govern (email provider, cache, Telegram, storage, HTTP API client…):
  abstraction in Application.Common, implementation folder in Infrastructure, options, DI, health check, test fake.
  Use when a feature needs to talk to something outside the process.
---

# Add an infrastructure concern

Reference implementation: email (`IEmailService` → `Infrastructure/Emailing/Services/LoggingEmailService.cs`, faked by `tests/Govern.IntegrationTests/Fixtures/FakeEmailService.cs`).

Read first: `architecture.md` (Infrastructure template), `security.md` (secrets), `logging.md`.

## 1. Abstraction: `src/Govern.Application.Common/Abstractions/I[Concern]Service.cs`
- Describe what the application needs ("send an email"), not the vendor API. Keep it small, async, with a `CancellationToken`.
- Put the messages and models it uses in `Application.Common/Models/`.

## 2. Implementation: `src/Govern.Infrastructure/[Concern]/`
- The folder uses only UF sub-folders: `Services/[Vendor][Concern]Service.cs` (`internal sealed`), `Options/[Concern]Options.cs`, `Models/`, and so on.
- **Options:** bound from the `[Concern]` config section with `ValidateDataAnnotations().ValidateOnStart()`. Secrets (API keys) come from user-secrets locally and Key Vault in Azure. Add them to `infra/modules/keyVault.bicep` and the Web App app settings as Key Vault references. Never put them in appsettings.
- **HTTP APIs:** a typed `HttpClient` via `services.AddHttpClient<...>()`, with a timeout and resilience (`Microsoft.Extensions.Http.Resilience`, added with `dotnet add package`). Never `new HttpClient()`.
- **Logging:** `[LoggerMessage]`, with no secrets or personal data in logs.

## 3. Registration
- In `src/Govern.Infrastructure/DependencyInjection.cs`, register the service. Match the lifetime to its dependencies: a singleton for stateless clients, scoped when it uses scoped services.
- If the application can't work without it, add a health check (`services.AddHealthChecks().Add...(name, tags: ["ready"])`).

## 4. Tests
- Integration tests never call the real external service. Add a fake in `tests/Govern.IntegrationTests/Fixtures/Fake[Concern]Service.cs` and swap it in `GovernApiFactory.ConfigureWebHost` (`RemoveAll` + `AddSingleton`). Assert on what the fake captured.
- Unit tests only if the logic lives in `Govern.Utils`.

## 5. Docs and infra
- `docs/operations/configuration.md`: the new config keys and where their values come from.
- `infra/`: the new Azure resources or secrets. Keep `az bicep build --file infra/main.bicep` clean.
- If the concern calls out to external services or handles credentials, run the `security-auditor` agent.
