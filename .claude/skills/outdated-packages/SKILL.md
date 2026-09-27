---
name: outdated-packages
description: >
  Checks Govern's NuGet packages for vulnerable, deprecated and outdated versions and upgrades them safely through
  central package management, then verifies. Use when the user asks to update/upgrade packages, when CI reports
  vulnerable packages, or periodically before a release.
disable-model-invocation: true
---

# Outdated packages

All versions live in `Directory.Packages.props` (central package management with transitive pinning). Never pick versions from memory; query NuGet.

## 1. Report
```bash
dotnet list Govern.slnx package --vulnerable --include-transitive
dotnet list Govern.slnx package --deprecated
dotnet list Govern.slnx package --outdated
```
Summarise the results as a table: package, current version, latest version, kind (security / major / minor / patch), projects.

## 2. Plan
Handle the categories in this order:
1. **Vulnerable** packages (security): upgrade first.
2. **Deprecated** packages: follow the suggested replacement.
3. **Patch and minor** versions: batch together.
4. **Major** versions: one package family per change. Read the release notes (context7 / microsoft-docs) and list the breaking changes before upgrading.

Keep families aligned:
- `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*` and `Microsoft.EntityFrameworkCore.*` move together.
- `Npgsql.EntityFrameworkCore.PostgreSQL` follows the EF Core major version.
- `Microsoft.CodeAnalysis.CSharp` is pinned to the Roslyn version required by `Microsoft.EntityFrameworkCore.Design`. Check its dependency before moving it.

Ask the user before any major upgrade.

## 3. Upgrade
- Edit the `Version` attributes in `Directory.Packages.props`, or use `dotnet add <project> package <Name>` without `--version` to get the latest.
- A restore hook runs automatically. NU1107/NU1608 conflicts mean a transitive pin disagrees: align the family or add an explicit pin with a comment explaining why.
- `dotnet-ef` in `dotnet-tools.json` should match the EF Core version.

## 4. Verify
Run `/verify`: build (warnings are errors), format, and all tests including the Testcontainers integration tests. Also check:
- `az bicep build --file infra/main.bicep`, when infra tooling changed
- `docker build` via `dotnet publish src/Govern.Api -t:PublishContainer`, when the runtime or SDK changed. Keep `global.json` and the container base image on the same major version.

## 5. Report
- The before and after table.
- Anything skipped, and why (e.g. a major upgrade waiting for approval).
- The test summary from the real run.
