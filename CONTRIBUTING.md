# Contributing to Govern

Thanks for your interest in Govern! Issues, ideas and pull requests are welcome. This page covers how to propose a change and what a change needs before it can be merged.

## Ways to contribute

- **Report a bug** or **suggest a feature:** [open an issue](https://github.com/andy-dendy-repo/govern/issues/new). For a bug, include the steps to reproduce, what you expected and what happened.
- **Ask a question or discuss the design:** open an issue. The [architecture](docs/architecture.md) and the [decision records](docs/decisions) explain why things are the way they are.
- **Send a pull request:** for anything bigger than a small fix, open an issue first so we can agree on the approach.
- **Report a security vulnerability:** **don't open a public issue**; follow [SECURITY.md](SECURITY.md).

## Setting up

You need the .NET SDK pinned in [`global.json`](global.json) and Docker (the integration tests start PostgreSQL in Testcontainers).

```bash
dotnet tool restore                 # local tools (dotnet-ef, …)
dotnet build Govern.slnx            # warnings are errors
dotnet test Govern.slnx             # all tests (needs Docker running)
```

To run the API and the client locally (database and user-secrets), follow [local development](docs/operations/local-development.md).

## Branches and pull requests

- `stage` is the integration branch and the base for all work. `main` is production, and every merge to it is a release.
- Branch from `stage` with a descriptive name (`feature/<name>`, `fix/<name>`, `docs/<name>`, `chore/<name>`), and open the PR **to `stage`**.
- Use [conventional commits](https://www.conventionalcommits.org/): `feat(games): …`, `fix(api): …`, `docs: …`, `test: …`, `chore: …`.
- Keep a PR to one change, and explain the *why* in its description.
- CI must pass. It runs the build, the format check, all tests, a vulnerable-package scan and a Bicep compile.

The full flow, including releases, is in [branching](docs/operations/branching.md).

## Definition of Done

A change is ready to merge when:

1. **It follows the conventions.** The project map, folder templates and naming rules are in [architecture](docs/architecture.md) and [`.claude/rules`](.claude/rules). Most of them are enforced by `Govern.ArchitectureTests`, so the build tells you when something is off.
2. **It's tested at the right level:**
   - **Unit tests** (`Govern.UnitTests`): Core, Domain and Utils only, which is pure code.
   - **Integration tests** (`Govern.IntegrationTests`): every endpoint over HTTP against real Postgres, covering the happy path, validation (400), auth (401/403) and domain errors.
   - **Client tests** (`Govern.ClientTests`): Blazor components and services with bUnit.
   - **Architecture tests:** when you add or change a convention.
3. **Every public member in `src/` has XML docs.** The build fails otherwise.
4. **The docs are updated:**
   - **Behaviour, endpoints or configuration:** update the matching page in [`docs/`](docs/README.md) and its index entry.
   - **Game rules:** also update the player [rules reference](docs/game/rules.md); its tables are checked against the ruleset by tests.
5. **Everything is green locally:**
   ```bash
   dotnet build Govern.slnx
   dotnet format Govern.slnx --verify-no-changes
   dotnet test Govern.slnx
   ```

## Conventions at a glance

- **Use cases:** one use case is one file, `Application/[Feature]/Contracts/XxxContract.cs`, holding Request, Response, Validator and Handler.
- **Endpoints:** one endpoint per file, in `Api/Features/[Feature]/XxxEndpoint.cs`.
- **Errors:** expected failures are returned as `Error` values, not exceptions.
- **Mapping:** no mapping libraries; use EF `Select` projections.
- **Names:** no `Helper` classes; name classes by their role.
- **Packages:** add them with `dotnet add package`. Versions live only in [`Directory.Packages.props`](Directory.Packages.props).
- **Game data:** game numbers live in the ruleset. Never edit a published `rulesets/<key>/vN.json`; add a new version instead (see [game data](docs/game/game-data.md)).
- **Migrations:** create them with `dotnet ef migrations add`, never by hand (see [`CLAUDE.md`](CLAUDE.md) for the exact command).

## Using AI tools

The repo ships a [Claude Code](https://claude.com/claude-code) setup (see [`CLAUDE.md`](CLAUDE.md) and [`.claude/`](.claude)) with rules, skills and safety hooks. You're welcome to use it or any other tool. Either way, you're responsible for the change, and the Definition of Done above applies all the same.

## License

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE).
